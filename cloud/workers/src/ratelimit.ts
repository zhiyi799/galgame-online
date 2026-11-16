// Per-IP fixed-window rate limiting backed by a KV namespace.
// Mirrors the C# prototype's auth limiter (10 requests / minute, shared across
// signup + login). If no KV binding is configured (local `wrangler dev`
// without the namespace), limiting is skipped so development is not blocked —
// production MUST bind RATE_LIMIT_KV (see wrangler.toml).

export interface RateLimitResult {
  allowed: boolean;
  /** Seconds until the window resets (only when not allowed). */
  retryAfterSec?: number;
}

interface Bucket {
  count: number;
  resetAt: number; // epoch ms
}

export async function checkRateLimit(
  kv: KVNamespace | undefined,
  key: string,
  limit: number,
  windowSec: number,
): Promise<RateLimitResult> {
  if (!kv) return { allowed: true };
  const now = Date.now();
  const raw = await kv.get(key);
  let bucket: Bucket | null = null;
  try {
    bucket = raw ? (JSON.parse(raw) as Bucket) : null;
  } catch {
    bucket = null;
  }
  if (!bucket || bucket.resetAt <= now) {
    const next: Bucket = { count: 1, resetAt: now + windowSec * 1000 };
    await kv.put(key, JSON.stringify(next), { expirationTtl: windowSec });
    return { allowed: true };
  }
  if (bucket.count >= limit) {
    return {
      allowed: false,
      retryAfterSec: Math.ceil((bucket.resetAt - now) / 1000),
    };
  }
  bucket.count += 1;
  await kv.put(key, JSON.stringify(bucket), {
    expirationTtl: Math.ceil((bucket.resetAt - now) / 1000),
  });
  return { allowed: true };
}

/** Client IP for rate limiting: CF-Connecting-IP, else the socket address. */
export function clientIp(request: Request): string {
  return (
    request.headers.get("CF-Connecting-IP") ??
    request.headers.get("X-Forwarded-For")?.split(",")[0]?.trim() ??
    "unknown"
  );
}
