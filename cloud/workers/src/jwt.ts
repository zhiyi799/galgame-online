// Supabase JWT verification (WebCrypto — no dependency).
//
// Supabase Auth access tokens come in two shapes, and the project decides
// which one it issues:
//   - HS256: signed with the project's JWT secret as the raw HMAC key
//     (UTF-8 bytes of the secret string as shown in the dashboard).
//   - ES256: signed with an asymmetric P-256 key. The public key is fetched
//     from the project's JWKS endpoint
//     (<SUPABASE_URL>/auth/v1/.well-known/jwks.json) and matched by `kid`.
//     New Supabase projects issue ES256 by default, so this is the primary
//     path; HS256 stays as a fallback for projects still on the legacy secret.
// Verified in both paths: signature, expiry, audience == "authenticated",
// issuer == <SUPABASE_URL>/auth/v1 (ES256 path only — HS256 callers may not
// pass a URL), sub present.

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
  supabaseUrl?: string,
): Promise<JwtResult> {
  const parts = token.split(".");
  if (parts.length !== 3) return { ok: false, error: "malformed token" };
  const [headerB64, payloadB64, sigB64] = parts as [string, string, string];

  let header: { alg?: string; kid?: string };
  let claims: JwtClaims;
  try {
    header = JSON.parse(new TextDecoder().decode(base64UrlDecode(headerB64)));
    claims = JSON.parse(new TextDecoder().decode(base64UrlDecode(payloadB64)));
  } catch {
    return { ok: false, error: "malformed token" };
  }

  if (header.alg === "HS256") {
    const expected = await hmacSha256(
      new TextEncoder().encode(secret),
      `${headerB64}.${payloadB64}`,
    );
    if (!bytesEqual(expected, base64UrlDecode(sigB64))) {
      return { ok: false, error: "bad signature" };
    }
  } else if (header.alg === "ES256") {
    const sigOk = await verifyEs256(headerB64, payloadB64, sigB64, header.kid, supabaseUrl);
    if (!sigOk.ok) return sigOk;
    if (typeof claims.iss !== "string" || claims.iss !== `${supabaseUrl}/auth/v1`) {
      return { ok: false, error: "wrong issuer" };
    }
  } else {
    return { ok: false, error: "unsupported alg" };
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

/** JWKS entry: the lib's JsonWebKey lacks `kid`, so add it. (`kty`/`crv`
 *  already exist on JsonWebKey here.) */
interface Es256Jwk extends JsonWebKey {
  kid?: string;
}

/** Module-level JWKS cache (per Worker isolate). Refreshed when stale or
 *  when the token's `kid` is not in the cached set (key rotation). */
let jwksCache: { url: string; keys: Es256Jwk[]; fetchedAt: number } | null = null;
const JWKS_TTL_MS = 10 * 60 * 1000;

async function fetchJwks(jwksUrl: string, forceRefresh: boolean): Promise<Es256Jwk[] | null> {
  const now = Date.now();
  if (
    !forceRefresh &&
    jwksCache &&
    jwksCache.url === jwksUrl &&
    now - jwksCache.fetchedAt < JWKS_TTL_MS
  ) {
    return jwksCache.keys;
  }
  let res: Response;
  try {
    res = await fetch(jwksUrl);
  } catch {
    return null;
  }
  if (!res.ok) return null;
  let data: { keys?: Es256Jwk[] };
  try {
    data = (await res.json()) as { keys?: Es256Jwk[] };
  } catch {
    return null;
  }
  if (!Array.isArray(data.keys)) return null;
  jwksCache = { url: jwksUrl, keys: data.keys, fetchedAt: now };
  return data.keys;
}

async function verifyEs256(
  headerB64: string,
  payloadB64: string,
  sigB64: string,
  kid: string | undefined,
  supabaseUrl: string | undefined,
): Promise<{ ok: true } | { ok: false; error: string }> {
  if (!supabaseUrl) return { ok: false, error: "missing supabase url" };
  if (!kid) return { ok: false, error: "missing kid" };
  const jwksUrl = `${supabaseUrl.replace(/\/$/, "")}/auth/v1/.well-known/jwks.json`;

  let keys = await fetchJwks(jwksUrl, false);
  let jwk = keys?.find((k) => k.kid === kid && k.kty === "EC" && k.crv === "P-256");
  if (!jwk) {
    // Key rotation: the cached set may predate the signing key.
    keys = await fetchJwks(jwksUrl, true);
    jwk = keys?.find((k) => k.kid === kid && k.kty === "EC" && k.crv === "P-256");
  }
  if (!jwk) return { ok: false, error: "unknown key" };

  let key: CryptoKey;
  try {
    key = await crypto.subtle.importKey(
      "jwk",
      jwk,
      { name: "ECDSA", namedCurve: "P-256" },
      false,
      ["verify"],
    );
  } catch {
    return { ok: false, error: "bad key" };
  }
  let valid = false;
  try {
    valid = await crypto.subtle.verify(
      { name: "ECDSA", hash: "SHA-256" },
      key,
      base64UrlDecode(sigB64) as BufferSource,
      new TextEncoder().encode(`${headerB64}.${payloadB64}`),
    );
  } catch {
    return { ok: false, error: "bad signature" };
  }
  if (!valid) return { ok: false, error: "bad signature" };
  return { ok: true };
}

/** Extract the Bearer token from an Authorization header, if present. */
export function bearerToken(request: Request): string | null {
  const auth = request.headers.get("Authorization");
  if (!auth || !/^Bearer /i.test(auth)) return null;
  const token = auth.slice("Bearer ".length).trim();
  return token.length > 0 ? token : null;
}
