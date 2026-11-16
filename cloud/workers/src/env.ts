// Worker environment bindings. Secrets are set with `wrangler secret put`
// (production) or .dev.vars (local dev); never hardcode real values.

export interface Env {
  /** Supabase project URL, e.g. https://abcdefghijklm.supabase.co */
  SUPABASE_URL: string;
  /** Supabase anon (public) key — Auth signup/sign-in only. */
  SUPABASE_ANON_KEY: string;
  /** Supabase service_role key — SERVER ONLY, bypasses RLS. */
  SUPABASE_SERVICE_ROLE_KEY: string;
  /** Supabase JWT secret — HS256 key for verifying Auth access tokens. */
  SUPABASE_JWT_SECRET: string;
  /** Synthetic email domain for username-only auth (default players.galgame.local). */
  AUTH_EMAIL_DOMAIN?: string;
  /** "true" enables the dev-only POST /api/game/mail/send route. */
  ALLOW_DEV_MAIL_SEND?: string;
  /** KV namespace for auth rate limiting (optional in local dev). */
  RATE_LIMIT_KV?: KVNamespace;
}
