# GalgameOnline — Cloudflare Workers + Supabase backend

Production backend for the Unity client (PC / Android / iOS) and the HTML
client. This replaces the C# three-service prototype (`../server/`) as the
live stack; the C# code stays in the repo untouched as the verified
reference for the game logic.

## Architecture

```
Unity client (PC/Android/iOS) ─┐
HTML galgame client            ├─ HTTPS ─> Cloudflare Workers (this repo)
                               │             ├─ request validation
                               │             ├─ Supabase JWT verify
                               │             ├─ gacha engine (TS port)
                               │             └─ rate limiting (KV)
                               │
                               ├─ Auth REST ─> Supabase Auth
                               │                 (username/password,
                               │                  JWT access tokens)
                               └─ PostgREST/RPC ─> Supabase Postgres
                                                    ├─ wallets, gacha_state,
                                                    │  inventory, mail,
                                                    │  cloud_saves, profiles
                                                    ├─ banners + items
                                                    │  (seeded catalog)
                                                    └─ RPC: do_gacha_pull,
                                                       claim_mail,
                                                       upsert_cloud_save,
                                                       send_mail, ensure_wallet
Game assets ────────────────────────────────> Supabase Storage buckets
(character art, CG, BGM, voice, scenes)        (public-read, CDN-cached;
                                                see "Asset storage" below)
```

**The rule that shapes everything:** the client never writes the database.
Gacha, currency, rewards, and payments run only in server-side functions —
the Worker and `security definer` Postgres RPCs. Row Level Security denies
direct writes from the `anon`/`authenticated` roles entirely; the Worker is
the only writer, using the `service_role` key server-side.

## Layout

```
cloud/
  README.md                  this file
  workers/                   Cloudflare Workers project (TypeScript)
    package.json             scripts: dev / test / build / check-sql / deploy
    wrangler.toml            Worker config (KV binding commented out until created)
    .dev.vars.example        placeholder secrets for local dev (copy to .dev.vars)
    src/
      index.ts               router + CORS + config guard
      env.ts                 Env bindings type
      gacha.ts               gacha engine (faithful port of C# GachaEngine)
      jwt.ts                 Supabase JWT (HS256) verification, WebCrypto
      supabase.ts            PostgREST + Auth REST helpers (no SDK)
      ratelimit.ts           per-IP fixed-window limiter (KV-backed)
      http.ts                JSON helpers, requireAuth, CORS
      routes/sdk.ts          /api/sdk/* (signup/login/session/status)
      routes/game.ts        /api/game/* + /api/gateway/allocate
    test/
      gacha.test.ts          engine: pity, 50/50, 120-guarantee, costs
      routes.test.ts         JWT verify + pull route with stubbed Supabase
    scripts/check-sql.mjs    SQL syntax check (libpg-query, no live DB)
  supabase/
    migrations/
      0001_schema.sql        tables, indexes, RLS enabled
      0002_policies.sql      RLS: authenticated SELECT own rows only
      0003_functions.sql     RPC: ensure_wallet, do_gacha_pull, claim_mail,
                             upsert_cloud_save, send_mail (+ EXECUTE grants)
      0004_seed.sql          2 banners + 14-item catalog (from the C# prototype)
```

## Endpoint → route → RPC mapping

Same paths and JSON shapes as the C# prototype, so the Unity client and the
existing HTML client share one contract.

| C# endpoint | Worker route | Supabase call |
| --- | --- | --- |
| POST /api/sdk/signup | POST /api/sdk/signup | Auth `/auth/v1/signup` (anon key) + `profiles` insert |
| POST /api/sdk/login | POST /api/sdk/login | Auth `/auth/v1/token?grant_type=password` |
| GET /api/sdk/session | GET /api/sdk/session | JWT verify (local, `SUPABASE_JWT_SECRET`) |
| GET /api/sdk/status | GET /api/sdk/status | `profiles` count (no session count — see Deviations) |
| GET /api/game/status | GET /api/game/status | — (health only) |
| GET /api/game/banners | GET /api/game/banners | `banners` SELECT (active) |
| GET /api/game/banner/{id} | GET /api/game/banner/:id | `banners` + `gacha_state` SELECT |
| POST /api/game/gacha/pull | POST /api/game/gacha/pull | `ensure_wallet` + `gacha_state` SELECT, then `do_gacha_pull` RPC |
| GET /api/game/wallet | GET /api/game/wallet | `ensure_wallet` RPC |
| GET /api/game/inventory | GET /api/game/inventory | `inventory` SELECT |
| GET /api/game/mail | GET /api/game/mail | `mail` SELECT (newest first) |
| POST /api/game/mail/{id}/claim | POST /api/game/mail/:id/claim | `claim_mail` RPC |
| POST /api/game/mail/send | POST /api/game/mail/send | `send_mail` RPC (dev-gated, see below) |
| POST /api/game/save | POST /api/game/save | `upsert_cloud_save` RPC |
| GET /api/game/save/{slot} | GET /api/game/save/:slot | `cloud_saves` SELECT |
| POST /api/gateway/allocate | POST /api/gateway/allocate | — (returns this Worker's origin; see Deviations) |

### Gacha pull, end to end

1. Worker verifies the JWT, loads the banner, wallet, and gacha state.
2. Worker checks count (1 or 10), daily limit, and funds (premium + free).
3. Worker draws with the TypeScript engine (same math as C#: 5★ base 0.8%,
   soft pity from 70, hard pity 90/10, 50% featured share, lost 50/50 sets
   Guaranteed, 120-pull featured guarantee; single 160, 10-pull 1600;
   premium spent first, then free).
4. Worker calls `do_gacha_pull` RPC with the expected (pre-pull) state, the
   new state, the outcomes, and the cost split. The RPC locks the wallet and
   state rows (`FOR UPDATE`), re-validates the daily limit and funds, applies
   the spend + state + inventory grants in one transaction, and returns
   `state_changed` if another pull committed first — the Worker then retries
   the whole pull (max 3 attempts). This replaces the C# prototype's
   per-account `SemaphoreSlim` locks.

### Auth: username-only, via Supabase Auth

Supabase Auth needs an email, so the Worker maps a username to a synthetic
address: `<username>@players.galgame.local` (`AUTH_EMAIL_DOMAIN`). No mail is
ever sent there, so **email confirmation must be OFF** in the Supabase
project (Dashboard > Authentication > Providers > Email > Confirm email).
The signup route detects the misconfiguration (signup returns a user but no
session) and answers with a 500 explaining it. Username rules mirror the C#
prototype: `^[A-Za-z0-9_]{3,32}$`, password ≥ 8 chars, duplicate → 409,
bad credentials → generic 401 (no user enumeration).

### Rate limiting

Auth routes (signup + login) share a 10 req/min per-IP fixed window, backed
by a KV namespace (`RATE_LIMIT_KV`). Without the binding (local dev) the
limiter is skipped. Create it with `wrangler kv namespace create
RATE_LIMIT_KV` and uncomment the `[[kv_namespaces]]` block in
`wrangler.toml`.

### Dev mail helper

`POST /api/game/mail/send` lets a user mail themselves (compensation /
testing), mirroring the C# prototype. It is disabled unless
`ALLOW_DEV_MAIL_SEND=true` (default `false` in `wrangler.toml`). In
production, mail is sent by server jobs calling `send_mail` directly, not by
players.

## Local dev

```bash
cd cloud/workers
npm install
cp .dev.vars.example .dev.vars   # fill in your Supabase project values
npm test                          # vitest: 28 tests
npm run build                     # tsc --noEmit
npm run check-sql                 # parse all migrations (syntax only)
npm run dev                       # wrangler dev on :8787
curl http://127.0.0.1:8787/api/status
```

Without Supabase values in `.dev.vars`, the Worker still boots; `/api/status`
and `/api/game/status` answer, everything else returns 503 `server not
configured`.

## Deployment checklist (needs the user's accounts)

Cloudflare — sign in with GitHub (the account the user already uses):

1. `cd cloud/workers && npx wrangler login` (GitHub OAuth in the browser).
2. Create the KV namespace for auth rate limiting:
   `npx wrangler kv namespace create RATE_LIMIT_KV` — paste the returned id
   into the `[[kv_namespaces]]` block in `wrangler.toml` and uncomment it.
3. Set secrets (never commit real values):
   ```
   npx wrangler secret put SUPABASE_URL
   npx wrangler secret put SUPABASE_ANON_KEY
   npx wrangler secret put SUPABASE_SERVICE_ROLE_KEY
   npx wrangler secret put SUPABASE_JWT_SECRET
   ```
   Find them in the Supabase dashboard (Settings > API): Project URL,
   anon public key, service_role key, JWT Secret.
4. `npx wrangler deploy` — note the `*.workers.dev` URL; that is the
   `gameServerUrl` the login response returns to clients.

Supabase — sign in with GitHub (also GitHub OAuth):

1. Create a project (any region; note the project ref for the URL).
2. SQL Editor: run `supabase/migrations/0001_schema.sql` through
   `0004_seed.sql` in order (or `supabase db push` if using the CLI).
3. Authentication > Providers > Email: turn OFF "Confirm email" (the
   synthetic `players.galgame.local` addresses can never confirm).
4. Create the asset buckets (below) and set them public.
5. Optional: set a custom `AUTH_EMAIL_DOMAIN` via `wrangler secret put`
   if you want a different synthetic domain (it only needs to be stable —
   changing it later orphans existing accounts).

## Asset storage

No real assets exist yet — this is the layout and URL scheme for when they do.
One public bucket per asset class (public-read so the CDN can cache them;
upload is service-role only, never from the client):

| Bucket | Path scheme | Example |
| --- | --- | --- |
| `character-art` | `characters/<character-id>/<variant>.png` | `characters/101/standing.png` |
| `cg` | `cg/<cg-id>.png` | `cg/opening-01.png` |
| `bgm` | `bgm/<track-id>.ogg` | `bgm/theme-main.ogg` |
| `voice` | `voice/<character-id>/<line-id>.ogg` | `voice/101/greeting-01.ogg` |
| `scenes` | `scenes/<scene-id>/...` | `scenes/school-rooftop/bg.png` |

Public URL form (Supabase Storage + CDN):
`https://<project-ref>.supabase.co/storage/v1/object/public/<bucket>/<path>`.
The Unity client fetches these on demand and caches locally; the Worker never
proxies asset bytes. When assets exist, add a small manifest endpoint
(`GET /api/game/assets/manifest`) that lists the current version + URLs per
asset id — not built yet, because there is nothing to list.

## Payments — not wired yet (deliberate)

No payment provider is chosen, so there is no payment code here — only the
shape it must take when one is (Stripe, etc.):

- The client opens the provider's checkout (or the Worker creates a checkout
  session and returns its URL); money never flows through the game client.
- The provider calls a Worker webhook (`POST /api/billing/webhook`, signature
  verified with the provider's webhook secret). The webhook calls a
  `grant_purchase` RPC that credits premium currency / items exactly once
  (idempotency key = provider event id, stored in a `billing_events` table).
- Currency is only ever granted by that RPC, never by a client request.

## Deviations from the C# prototype (deliberate)

- **Auth is Supabase Auth, not Argon2id + session tokens.** No password
  hashing in the Worker; login returns a Supabase JWT (1h, same TTL as the
  C# session). `GET /api/sdk/session` verifies the JWT locally instead of
  looking a token up in a sessions table.
- **QueryGateway is not ported.** Workers are serverless — there are no
  game-server instances to register or allocate. `POST
  /api/gateway/allocate` exists only as a compatibility route returning this
  Worker's origin; the login response's `gameServerUrl` already points the
  client here.
- **`/api/sdk/status` has no session count.** Supabase Auth sessions are not
  countable from the Worker; it reports `{ok, accounts}` only.
- **Banners and the item catalog live in Postgres** (`banners`, `items`
  tables, seeded), not in code — the Worker reads them per request.
- **Cloud saves are stored as `text`** (same as the C# `DataJson` string),
  not `jsonb`, so any save blob the client sends is accepted.
- **`dailyLeft` is 0 for unlimited banners** — the C# quirk is kept so the
  HTML client's display logic does not change.
- **The Worker computes the draw; the RPC commits it.** The C# prototype did
  both in one process. Splitting keeps the engine unit-testable in
  TypeScript while the RPC keeps the commit atomic.

## Verification status (2026-11-16, deployed and verified live)

- `npm test`: **33/33 pass** (gacha engine pity/50-50/120-guarantee/costs;
  JWT sign/verify incl. ES256-via-JWKS; pull route with stubbed Supabase:
  insufficient funds, bad count, commit, OCC retry, unauthenticated).
- `npm run build` (`tsc --noEmit`): clean.
- `npm run check-sql`: all 4 migrations parse (libpg-query), including the
  5 plpgsql function bodies. Syntax only at scaffold time — **since executed
  against the live Supabase project** (see below).
- **Deployed 2026-11-16 ~13:36 CST**: Worker `galgame-online-api` at
  `https://galgame-online-api.2119351988.workers.dev` (Cloudflare account
  2119351988@qq.com, API token `galgame-online-wrangler`). KV namespace
  `RATE_LIMIT_KV` created and bound; 4 secrets set (SUPABASE_URL,
  SUPABASE_ANON_KEY, SUPABASE_SERVICE_ROLE_KEY, SUPABASE_JWT_SECRET).
- **Supabase project `galgame-online`** (ref `bfhjmnyzofcgodvpivnm`,
  Singapore, Free): migrations 0001–0004 run in the SQL Editor, email
  confirmation OFF, 5 public buckets
  (character-art, cg, bgm, voice, scenes).
- **Live flow verified 2026-11-16 ~13:40 CST** against the deployed Worker:
  signup → login → session → banners (2) → wallet (0/1000/0) → single
  pull (3★ item 301, pity counters move) → inventory (item 301 ×1) →
  wallet after pull (free 840). All good.
- **ES256 fix (2026-11-16)**: new Supabase projects issue ES256 access
  tokens, not HS256. The Worker verifies ES256 against the project's JWKS
  endpoint (`<SUPABASE_URL>/auth/v1/.well-known/jwks.json`, cached 10 min,
  refetch on unknown `kid`); HS256 with `SUPABASE_JWT_SECRET` stays as a
  fallback. Before this fix every authed call returned `unauthorized`
  even though login succeeded.
- **Deployment notes**: `wrangler login` OAuth does NOT work from the
  sandbox — the cloud browser cannot reach the CLI's localhost:8976
  callback (ERR_CONNECTION_REFUSED). Deploy with a Cloudflare API token
  instead (`CLOUDFLARE_API_TOKEN` + `CLOUDFLARE_ACCOUNT_ID` env vars;
  "Edit Cloudflare Workers" template). Copy the token from the main value
  display on the creation page, not from the example command (a bad copy
  fails with "Invalid API Token").
