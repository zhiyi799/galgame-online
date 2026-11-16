// GalgameOnline API — Cloudflare Workers entry point.
// The ONLY business entry for the Unity client and the HTML client.
// Routes mirror the C# prototype's paths so both clients share one contract.
//
//   POST /api/sdk/signup          {username, password}
//   POST /api/sdk/login           {username, password} -> {token, expiresAt, gameServerUrl}
//   GET  /api/sdk/session        Bearer -> {ok, accountId}
//   GET  /api/sdk/status
//   GET  /api/game/status
//   GET  /api/game/banners
//   GET  /api/game/banner/:id
//   POST /api/game/gacha/pull    {bannerId, count: 1|10}
//   GET  /api/game/wallet
//   GET  /api/game/inventory
//   GET  /api/game/mail
//   POST /api/game/mail/:id/claim
//   POST /api/game/mail/send     (dev-only, ALLOW_DEV_MAIL_SEND=true)
//   POST /api/game/save          {slot: 1-3, dataJson}
//   GET  /api/game/save/:slot
//   POST /api/gateway/allocate   (compat: returns this Worker's origin)
//
// Everything except /status routes needs Supabase configured (SUPABASE_URL,
// SUPABASE_ANON_KEY, SUPABASE_SERVICE_ROLE_KEY, SUPABASE_JWT_SECRET).
// Without them the Worker still boots and answers /api/status.

import type { Env } from "./env.js";
import { CORS_HEADERS, err, json, withCors } from "./http.js";
import {
  allocate,
  bannerDetail,
  banners,
  claimMail,
  downloadSave,
  gachaPull,
  gameStatus,
  inventory,
  mail,
  sendMail,
  uploadSave,
  wallet,
} from "./routes/game.js";
import { login, sessionCheck, signup, status as sdkStatus } from "./routes/sdk.js";

type Handler = (request: Request, env: Env, params: Record<string, string>) => Promise<Response>;

interface Route {
  method: string;
  pattern: RegExp;
  keys: string[];
  handler: Handler;
  /** False for routes that work without Supabase configured (health checks). */
  needsConfig: boolean;
}

function route(
  method: string,
  path: string,
  handler: Handler,
  needsConfig = true,
): Route {
  const keys: string[] = [];
  const pattern = new RegExp(
    "^" +
      path.replace(/:[a-zA-Z]+/g, (m) => {
        keys.push(m.slice(1));
        return "([^/]+)";
      }) +
      "$",
  );
  return { method, pattern, keys, handler, needsConfig };
}

const ROUTES: Route[] = [
  route("POST", "/api/sdk/signup", (req, env) => signup(req, env)),
  route("POST", "/api/sdk/login", (req, env) => login(req, env)),
  route("GET", "/api/sdk/session", (req, env) => sessionCheck(req, env)),
  route("GET", "/api/sdk/status", (req, env) => sdkStatus(req, env)),
  route("GET", "/api/game/status", () => gameStatus(), false),
  route("GET", "/api/game/banners", (req, env) => banners(req, env)),
  route("GET", "/api/game/banner/:id", (req, env, p) =>
    bannerDetail(req, env, Number(p["id"])),
  ),
  route("POST", "/api/game/gacha/pull", (req, env) => gachaPull(req, env)),
  route("GET", "/api/game/wallet", (req, env) => wallet(req, env)),
  route("GET", "/api/game/inventory", (req, env) => inventory(req, env)),
  route("GET", "/api/game/mail", (req, env) => mail(req, env)),
  route("POST", "/api/game/mail/:id/claim", (req, env, p) =>
    claimMail(req, env, p["id"] as string),
  ),
  route("POST", "/api/game/mail/send", (req, env) => sendMail(req, env)),
  route("POST", "/api/game/save", (req, env) => uploadSave(req, env)),
  route("GET", "/api/game/save/:slot", (req, env, p) =>
    downloadSave(req, env, Number(p["slot"])),
  ),
  route("POST", "/api/gateway/allocate", (req, env) => allocate(req, env)),
];

function configMissing(env: Env): boolean {
  return (
    !env.SUPABASE_URL ||
    !env.SUPABASE_ANON_KEY ||
    !env.SUPABASE_SERVICE_ROLE_KEY ||
    !env.SUPABASE_JWT_SECRET
  );
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);

    if (request.method === "OPTIONS") {
      return new Response(null, { status: 204, headers: CORS_HEADERS });
    }

    if (url.pathname === "/api/status") {
      return withCors(json({ ok: true, service: "galgame-online-api" }));
    }

    for (const r of ROUTES) {
      if (r.method !== request.method) continue;
      const m = r.pattern.exec(url.pathname);
      if (!m) continue;
      const params: Record<string, string> = {};
      r.keys.forEach((k, i) => {
        params[k] = m[i + 1] as string;
      });
      try {
        if (r.needsConfig && configMissing(env)) {
          return withCors(err("server not configured", 503));
        }
        return withCors(await r.handler(request, env, params));
      } catch (e) {
        console.error("unhandled error", e);
        return withCors(err("internal error", 500));
      }
    }
    return withCors(err("not found", 404));
  },
};
