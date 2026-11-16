// Small HTTP helpers shared by the routes.

import type { Env } from "./env.js";
import { bearerToken, verifySupabaseJwt } from "./jwt.js";

export function json(data: unknown, status = 200, extraHeaders: Record<string, string> = {}): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: {
      "Content-Type": "application/json",
      ...extraHeaders,
    },
  });
}

export function err(error: string, status = 400): Response {
  return json({ ok: false, error }, status);
}

/** Parse a JSON body; returns null when the body is missing or invalid. */
export async function parseJson<T>(request: Request): Promise<T | null> {
  try {
    return (await request.json()) as T;
  } catch {
    return null;
  }
}

export type AuthResult = { userId: string } | { response: Response };

/**
 * Authenticate a protected route: verify the Supabase JWT from the
 * Authorization header. Returns the user id (Supabase auth.users id), or a
 * 401 Response to return directly.
 */
export async function requireAuth(request: Request, env: Env): Promise<AuthResult> {
  const token = bearerToken(request);
  if (!token) return { response: err("unauthorized", 401) };
  const res = await verifySupabaseJwt(token, env.SUPABASE_JWT_SECRET);
  if (!res.ok) return { response: err("unauthorized", 401) };
  return { userId: res.sub };
}

/** CORS headers for browser clients (the HTML galgame client). Native Unity
 *  clients ignore CORS, but the same Worker serves both. Auth is by Bearer
 *  token, so a permissive origin is acceptable here. */
export const CORS_HEADERS: Record<string, string> = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
  "Access-Control-Allow-Headers": "Authorization, Content-Type",
  "Access-Control-Max-Age": "86400",
};

export function withCors(res: Response): Response {
  const headers = new Headers(res.headers);
  for (const [k, v] of Object.entries(CORS_HEADERS)) headers.set(k, v);
  return new Response(res.body, { status: res.status, headers });
}
