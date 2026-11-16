// Tests for JWT verification (src/jwt.ts) and the pull route's spend/commit
// logic (src/routes/game.ts) with Supabase stubbed out via global fetch.

import { createHmac } from "node:crypto";
import { afterEach, describe, expect, it } from "vitest";
import { verifySupabaseJwt } from "../src/jwt.js";
import type { Env } from "../src/env.js";
import { gachaPull } from "../src/routes/game.js";

const SECRET = "test-jwt-secret";

function b64url(input: string): string {
  return Buffer.from(input).toString("base64url");
}

function signToken(payload: Record<string, unknown>, secret = SECRET): string {
  const header = b64url(JSON.stringify({ alg: "HS256", typ: "JWT" }));
  const body = b64url(JSON.stringify(payload));
  const sig = createHmac("sha256", secret).update(`${header}.${body}`).digest("base64url");
  return `${header}.${body}.${sig}`;
}

function validPayload(overrides: Record<string, unknown> = {}): Record<string, unknown> {
  const now = Math.floor(Date.now() / 1000);
  return {
    sub: "00000000-0000-0000-0000-000000000001",
    aud: "authenticated",
    exp: now + 3600,
    iat: now,
    iss: "https://test.supabase.co/auth/v1",
    ...overrides,
  };
}

describe("verifySupabaseJwt", () => {
  it("accepts a well-formed token", async () => {
    const res = await verifySupabaseJwt(signToken(validPayload()), SECRET);
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.sub).toBe("00000000-0000-0000-0000-000000000001");
  });

  it("rejects an expired token", async () => {
    const res = await verifySupabaseJwt(
      signToken(validPayload({ exp: Math.floor(Date.now() / 1000) - 10 })),
      SECRET,
    );
    expect(res).toEqual({ ok: false, error: "token expired" });
  });

  it("rejects the wrong audience", async () => {
    const res = await verifySupabaseJwt(signToken(validPayload({ aud: "service_role" })), SECRET);
    expect(res).toEqual({ ok: false, error: "wrong audience" });
  });

  it("rejects a tampered payload", async () => {
    const token = signToken(validPayload());
    const [h, p, s] = token.split(".");
    const tampered = `${h}.${b64url(JSON.stringify(validPayload({ sub: "attacker" })))}.${s}`;
    const res = await verifySupabaseJwt(tampered, SECRET);
    expect(res).toEqual({ ok: false, error: "bad signature" });
  });

  it("rejects a token signed with another secret", async () => {
    const res = await verifySupabaseJwt(signToken(validPayload(), "other-secret"), SECRET);
    expect(res).toEqual({ ok: false, error: "bad signature" });
  });

  it("rejects a malformed token", async () => {
    const res = await verifySupabaseJwt("not-a-token", SECRET);
    expect(res).toEqual({ ok: false, error: "malformed token" });
  });
});

// --- ES256 (JWKS) verification ---

const SUPABASE_URL = "https://test.supabase.co";

async function makeEs256Key(kid: string) {
  const pair = (await crypto.subtle.generateKey(
    { name: "ECDSA", namedCurve: "P-256" },
    true,
    ["sign", "verify"],
  )) as CryptoKeyPair;
  const jwk = await crypto.subtle.exportKey("jwk", pair.publicKey);
  const jwks = { keys: [{ ...jwk, kid, alg: "ES256", use: "sig" }] };
  return { privateKey: pair.privateKey, jwks };
}

async function signEs256Token(
  payload: Record<string, unknown>,
  privateKey: CryptoKey,
  kid: string,
): Promise<string> {
  const header = b64url(JSON.stringify({ alg: "ES256", typ: "JWT", kid }));
  const body = b64url(JSON.stringify(payload));
  const sig = await crypto.subtle.sign(
    { name: "ECDSA", hash: "SHA-256" },
    privateKey,
    new TextEncoder().encode(`${header}.${body}`),
  );
  return `${header}.${body}.${Buffer.from(sig).toString("base64url")}`;
}

function stubJwks(jwks: unknown): () => void {
  const originalFetch = globalThis.fetch;
  globalThis.fetch = (async (input: string | URL | Request) => {
    const url = String(input);
    if (url.includes("/.well-known/jwks.json")) {
      return new Response(JSON.stringify(jwks), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    }
    return new Response(JSON.stringify({ error: `unexpected URL ${url}` }), { status: 404 });
  }) as typeof fetch;
  return () => void (globalThis.fetch = originalFetch);
}

let es256KidCounter = 0;

describe("verifySupabaseJwt (ES256 via JWKS)", () => {
  let restore = () => {};

  afterEach(() => {
    restore();
  });

  it("accepts a well-formed ES256 token", async () => {
    const kid = `test-kid-${++es256KidCounter}`;
    const { privateKey, jwks } = await makeEs256Key(kid);
    restore = stubJwks(jwks);
    const token = await signEs256Token(validPayload(), privateKey, kid);
    const res = await verifySupabaseJwt(token, SECRET, SUPABASE_URL);
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.sub).toBe("00000000-0000-0000-0000-000000000001");
  });

  it("rejects an ES256 token with an unknown kid", async () => {
    const kid = `test-kid-${++es256KidCounter}`;
    const { privateKey, jwks } = await makeEs256Key(kid);
    restore = stubJwks(jwks);
    const token = await signEs256Token(validPayload(), privateKey, "some-other-kid");
    const res = await verifySupabaseJwt(token, SECRET, SUPABASE_URL);
    expect(res).toEqual({ ok: false, error: "unknown key" });
  });

  it("rejects an ES256 token with a tampered payload", async () => {
    const kid = `test-kid-${++es256KidCounter}`;
    const { privateKey, jwks } = await makeEs256Key(kid);
    restore = stubJwks(jwks);
    const token = await signEs256Token(validPayload(), privateKey, kid);
    const [h, _p, s] = token.split(".");
    const tampered = `${h}.${b64url(JSON.stringify(validPayload({ sub: "attacker" })))}.${s}`;
    const res = await verifySupabaseJwt(tampered, SECRET, SUPABASE_URL);
    expect(res).toEqual({ ok: false, error: "bad signature" });
  });

  it("rejects an expired ES256 token", async () => {
    const kid = `test-kid-${++es256KidCounter}`;
    const { privateKey, jwks } = await makeEs256Key(kid);
    restore = stubJwks(jwks);
    const token = await signEs256Token(
      validPayload({ exp: Math.floor(Date.now() / 1000) - 10 }),
      privateKey,
      kid,
    );
    const res = await verifySupabaseJwt(token, SECRET, SUPABASE_URL);
    expect(res).toEqual({ ok: false, error: "token expired" });
  });

  it("rejects an ES256 token from the wrong issuer", async () => {
    const kid = `test-kid-${++es256KidCounter}`;
    const { privateKey, jwks } = await makeEs256Key(kid);
    restore = stubJwks(jwks);
    const token = await signEs256Token(
      validPayload({ iss: "https://other.supabase.co/auth/v1" }),
      privateKey,
      kid,
    );
    const res = await verifySupabaseJwt(token, SECRET, SUPABASE_URL);
    expect(res).toEqual({ ok: false, error: "wrong issuer" });
  });
});

// --- Pull route with a stubbed Supabase ---

const USER_ID = "00000000-0000-0000-0000-000000000001";

function testEnv(): Env {
  return {
    SUPABASE_URL: "https://test.supabase.co",
    SUPABASE_ANON_KEY: "anon-key",
    SUPABASE_SERVICE_ROLE_KEY: "service-key",
    SUPABASE_JWT_SECRET: SECRET,
    AUTH_EMAIL_DOMAIN: "players.galgame.local",
    ALLOW_DEV_MAIL_SEND: "true",
  };
}

function authedRequest(body: unknown): Request {
  return new Request("https://api.example.com/api/game/gacha/pull", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${signToken(validPayload())}`,
    },
    body: JSON.stringify(body),
  });
}

interface FetchCall {
  url: string;
  body: unknown;
}

function stubSupabase(opts: {
  wallet?: { premium: number; free: number; tickets: number };
  commitResults?: ({ ok: boolean; error?: string } | null)[];
}): { calls: FetchCall[]; restore: () => void } {
  const calls: FetchCall[] = [];
  const wallet = opts.wallet ?? { premium: 0, free: 1000, tickets: 0 };
  const commitQueue = [...(opts.commitResults ?? [{ ok: true }])];
  const originalFetch = globalThis.fetch;

  globalThis.fetch = (async (input: string | URL | Request, init?: RequestInit) => {
    const url = String(input);
    let body: unknown = null;
    try {
      body = init?.body ? JSON.parse(String(init.body)) : null;
    } catch {
      body = null;
    }
    calls.push({ url, body });

    const jsonResponse = (data: unknown, status = 200) =>
      new Response(JSON.stringify(data), { status, headers: { "Content-Type": "application/json" } });

    if (url.includes("/rest/v1/banners")) {
      return jsonResponse([
        {
          id: 1,
          name: "星夜邂逅",
          featured_five_star: 101,
          standard_five_star: [102, 103, 104],
          four_star: [201, 202, 203, 204],
          three_star: [301, 302, 303],
          five_star_pity: 90,
          four_star_pity: 10,
          cost_per_pull: 160,
          daily_limit: 0,
          active: true,
        },
      ]);
    }
    if (url.includes("/rest/v1/rpc/ensure_wallet")) {
      return jsonResponse({ user_id: USER_ID, ...wallet });
    }
    if (url.includes("/rest/v1/gacha_state")) {
      return jsonResponse([]);
    }
    if (url.includes("/rest/v1/rpc/do_gacha_pull")) {
      const next = commitQueue.shift();
      if (!next) return jsonResponse({ ok: false, error: "no stubbed commit left" });
      return jsonResponse(next);
    }
    return jsonResponse({ error: `unexpected URL ${url}` }, 404);
  }) as typeof fetch;

  return { calls, restore: () => void (globalThis.fetch = originalFetch) };
}

describe("POST /api/game/gacha/pull", () => {
  let restore = () => {};

  afterEach(() => {
    restore();
  });

  it("rejects a 10-pull when the wallet cannot cover it, without committing", async () => {
    const stub = stubSupabase({ wallet: { premium: 0, free: 1000, tickets: 0 } });
    restore = stub.restore;
    const res = await gachaPull(authedRequest({ bannerId: 1, count: 10 }), testEnv());
    const data = (await res.json()) as { ok: boolean; error: string; pulls: unknown };
    expect(data.ok).toBe(false);
    expect(data.error).toMatch(/insufficient currency: need 1600, have 1000/);
    expect(data.pulls).toBeNull();
    expect(stub.calls.some((c) => c.url.includes("do_gacha_pull"))).toBe(false);
  });

  it("rejects count other than 1 or 10 before touching Supabase", async () => {
    const stub = stubSupabase({});
    restore = stub.restore;
    const res = await gachaPull(authedRequest({ bannerId: 1, count: 3 }), testEnv());
    const data = (await res.json()) as { ok: boolean; error: string };
    expect(data.ok).toBe(false);
    expect(data.error).toBe("count must be 1 or 10");
    expect(stub.calls).toHaveLength(0);
  });

  it("commits a 10-pull and returns 10 outcomes with updated pity", async () => {
    const stub = stubSupabase({ wallet: { premium: 0, free: 2000, tickets: 0 } });
    restore = stub.restore;
    const res = await gachaPull(authedRequest({ bannerId: 1, count: 10 }), testEnv());
    const data = (await res.json()) as {
      ok: boolean;
      pulls: { itemId: number; rarity: number }[];
      banner: { totalCount: number; fiveRemain: number; fourRemain: number };
    };
    expect(data.ok).toBe(true);
    expect(data.pulls).toHaveLength(10);
    for (const p of data.pulls) expect([3, 4, 5]).toContain(p.rarity);
    expect(data.banner.totalCount).toBe(10);

    const commit = stub.calls.find((c) => c.url.includes("do_gacha_pull"));
    expect(commit).toBeDefined();
    const args = commit?.body as Record<string, unknown>;
    expect(args.p_cost_premium).toBe(0);
    expect(args.p_cost_free).toBe(1600);
    expect((args.p_outcomes as unknown[]).length).toBe(10);
    expect((args.p_new_state as { total: number }).total).toBe(10);
  });

  it("retries once when the commit reports a stale state, then succeeds", async () => {
    const stub = stubSupabase({
      wallet: { premium: 0, free: 2000, tickets: 0 },
      commitResults: [{ ok: false, error: "state_changed" }, { ok: true }],
    });
    restore = stub.restore;
    const res = await gachaPull(authedRequest({ bannerId: 1, count: 1 }), testEnv());
    const data = (await res.json()) as { ok: boolean; pulls: unknown[] };
    expect(data.ok).toBe(true);
    expect(data.pulls).toHaveLength(1);
    expect(stub.calls.filter((c) => c.url.includes("do_gacha_pull"))).toHaveLength(2);
  });

  it("rejects an unauthenticated pull", async () => {
    const stub = stubSupabase({});
    restore = stub.restore;
    const req = new Request("https://api.example.com/api/game/gacha/pull", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ bannerId: 1, count: 1 }),
    });
    const res = await gachaPull(req, testEnv());
    expect(res.status).toBe(401);
    expect(stub.calls).toHaveLength(0);
  });
});
