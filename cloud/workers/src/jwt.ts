// Supabase JWT verification (HS256, WebCrypto — no dependency).
// Supabase Auth signs access tokens with the project's JWT secret as the raw
// HMAC key (UTF-8 bytes of the secret string as shown in the dashboard).
// Verified: signature, expiry, audience == "authenticated".

export interface JwtClaims {
  sub: string;
  aud: string;
  exp: number;
  iat: number;
  iss: string;
  email?: string;
}

export type JwtResult =
  | { ok: true; sub: string; claims: JwtClaims }
  | { ok: false; error: string };

function base64UrlDecode(input: string): Uint8Array {
  const padded = input.replace(/-/g, "+").replace(/_/g, "/");
  const pad = padded.length % 4 === 0 ? "" : "=".repeat(4 - (padded.length % 4));
  const binary = atob(padded + pad);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes;
}

async function hmacSha256(keyBytes: Uint8Array, data: string): Promise<Uint8Array> {
  const key = await crypto.subtle.importKey(
    "raw",
    keyBytes as BufferSource,
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
  const sig = await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(data));
  return new Uint8Array(sig);
}

function bytesEqual(a: Uint8Array, b: Uint8Array): boolean {
  if (a.length !== b.length) return false;
  let diff = 0;
  for (let i = 0; i < a.length; i++) diff |= (a[i] as number) ^ (b[i] as number);
  return diff === 0;
}

export async function verifySupabaseJwt(
  token: string,
  secret: string,
): Promise<JwtResult> {
  const parts = token.split(".");
  if (parts.length !== 3) return { ok: false, error: "malformed token" };
  const [headerB64, payloadB64, sigB64] = parts as [string, string, string];

  let header: { alg?: string };
  let claims: JwtClaims;
  try {
    header = JSON.parse(new TextDecoder().decode(base64UrlDecode(headerB64)));
    claims = JSON.parse(new TextDecoder().decode(base64UrlDecode(payloadB64)));
  } catch {
    return { ok: false, error: "malformed token" };
  }
  if (header.alg !== "HS256") return { ok: false, error: "unsupported alg" };

  const expected = await hmacSha256(
    new TextEncoder().encode(secret),
    `${headerB64}.${payloadB64}`,
  );
  if (!bytesEqual(expected, base64UrlDecode(sigB64))) {
    return { ok: false, error: "bad signature" };
  }

  const nowSec = Math.floor(Date.now() / 1000);
  if (typeof claims.exp !== "number" || claims.exp <= nowSec) {
    return { ok: false, error: "token expired" };
  }
  if (claims.aud !== "authenticated") {
    return { ok: false, error: "wrong audience" };
  }
  if (typeof claims.sub !== "string" || claims.sub.length === 0) {
    return { ok: false, error: "missing sub" };
  }
  return { ok: true, sub: claims.sub, claims };
}

/** Extract the Bearer token from an Authorization header, if present. */
export function bearerToken(request: Request): string | null {
  const auth = request.headers.get("Authorization");
  if (!auth || !/^Bearer /i.test(auth)) return null;
  const token = auth.slice("Bearer ".length).trim();
  return token.length > 0 ? token : null;
}
