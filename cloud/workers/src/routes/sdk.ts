// Auth routes — /api/sdk/*
// Auth itself is Supabase Auth (password grant). The Worker never stores or
// hashes passwords. Username-only login is mapped to a synthetic email:
//   <username>@<AUTH_EMAIL_DOMAIN>  (default players.galgame.local)
// Email confirmation MUST be disabled in the Supabase project (see README),
// because no mail is ever sent to the synthetic domain.
//
// Validation mirrors the C# prototype: username ^[A-Za-z0-9_]{3,32}$,
// password >= 8 chars, duplicate username -> 409, bad credentials -> 401
// (same generic error for unknown user and wrong password).

import type { Env } from "../env.js";
import { err, json, parseJson } from "../http.js";
import { bearerToken, verifySupabaseJwt } from "../jwt.js";
import { checkRateLimit, clientIp } from "../ratelimit.js";
import {
  SupabaseError,
  authSignIn,
  authSignUp,
  countRows,
  createProfile,
  touchLastLogin,
} from "../supabase.js";
import type { LoginRequest, SignupRequest } from "../types.js";

const USERNAME_RE = /^[A-Za-z0-9_]{3,32}$/;
const AUTH_LIMIT = 10; // requests per minute per IP, shared signup + login
const AUTH_WINDOW_SEC = 60;

function syntheticEmail(env: Env, username: string): string {
  const domain = env.AUTH_EMAIL_DOMAIN ?? "players.galgame.local";
  return `${username}@${domain}`;
}

function validateCredentials(body: { username?: unknown; password?: unknown }): string | null {
  if (typeof body.username !== "string" || body.username.trim() === "" || typeof body.password !== "string") {
    return "Username and password are required.";
  }
  if (!USERNAME_RE.test(body.username)) {
    return "Username must be 3-32 characters: letters, digits, underscore.";
  }
  if (body.password.length < 8) {
    return "Password must be at least 8 characters.";
  }
  return null;
}

async function rateLimitAuth(request: Request, env: Env): Promise<Response | null> {
  const res = await checkRateLimit(
    env.RATE_LIMIT_KV,
    `auth:${clientIp(request)}`,
    AUTH_LIMIT,
    AUTH_WINDOW_SEC,
  );
  if (!res.allowed) {
    return json(
      { ok: false, error: "Too many requests, try again later." },
      429,
      { "Retry-After": String(res.retryAfterSec ?? AUTH_WINDOW_SEC) },
    );
  }
  return null;
}

export async function signup(request: Request, env: Env): Promise<Response> {
  const limited = await rateLimitAuth(request, env);
  if (limited) return limited;

  const body = await parseJson<SignupRequest>(request);
  if (!body) return err("Username and password are required.", 400);
  const invalid = validateCredentials(body);
  if (invalid) return err(invalid, 400);

  const email = syntheticEmail(env, body.username);
  let result;
  try {
    result = await authSignUp(env, email, body.password);
  } catch (e) {
    if (e instanceof SupabaseError) return err("Signup failed, try again later.", 502);
    throw e;
  }
  if (!result.ok) {
    // GoTrue reports a duplicate as "User already registered".
    if (/already registered/i.test(result.error)) {
      return err("Username is already taken.", 409);
    }
    return err(result.error, 400);
  }
  if (!result.hasSession) {
    // Email confirmation is ON in the Supabase project — the synthetic
    // address can never confirm. This is a deployment misconfiguration.
    return err(
      "Email confirmation is enabled in Supabase Auth; disable it (see cloud README).",
      500,
    );
  }
  // Profile row for the public username. Best effort: the Auth user already
  // exists, so a failure here must not turn signup into an orphan-retry loop.
  try {
    await createProfile(env, result.user.id, body.username);
  } catch {
    // Logged by the platform; the profile is backfilled on first login read.
  }
  return json({ ok: true, accountId: result.user.id });
}

export async function login(request: Request, env: Env): Promise<Response> {
  const limited = await rateLimitAuth(request, env);
  if (limited) return limited;

  const body = await parseJson<LoginRequest>(request);
  if (!body) return err("Username and password are required.", 400);
  const invalid = validateCredentials(body);
  if (invalid) return err(invalid, 400);

  const email = syntheticEmail(env, body.username);
  let result;
  try {
    result = await authSignIn(env, email, body.password);
  } catch (e) {
    if (e instanceof SupabaseError) return err("Login failed, try again later.", 502);
    throw e;
  }
  if (!result.ok) {
    // Same generic error for unknown user and wrong password (no enumeration).
    return err("Invalid username or password.", 401);
  }

  // The Worker IS the game server in this architecture, so the login response
  // points the client back at this Worker's origin.
  const gameServerUrl = new URL(request.url).origin;
  await touchLastLogin(env, result.session.user.id);
  return json({
    ok: true,
    token: result.session.accessToken,
    expiresAt: new Date(Date.now() + result.session.expiresIn * 1000).toISOString(),
    gameServerUrl,
  });
}

export async function sessionCheck(request: Request, env: Env): Promise<Response> {
  // Same envelope as the C# prototype: 200 with {ok, accountId}.
  const token = bearerToken(request);
  if (!token) return json({ ok: false, accountId: null });
  const res = await verifySupabaseJwt(token, env.SUPABASE_JWT_SECRET);
  if (!res.ok) return json({ ok: false, accountId: null });
  return json({ ok: true, accountId: res.sub });
}

export async function status(_request: Request, env: Env): Promise<Response> {
  // Deviation from the C# prototype: Supabase Auth sessions are not
  // countable from the Worker, so only the account count is reported.
  try {
    const accounts = await countRows(env, "profiles");
    return json({ ok: true, accounts });
  } catch (e) {
    if (e instanceof SupabaseError) return err("status unavailable", 502);
    throw e;
  }
}
