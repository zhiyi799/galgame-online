// Minimal Supabase client over plain fetch (no SDK dependency).
// The Worker is the ONLY caller of Supabase: it uses the service_role key for
// PostgREST (RLS is bypassed for service_role; all game-state writes go
// through security-definer RPC) and the anon key for Auth signup/sign-in.
// The Unity/HTML clients never see either key.

import type { Env } from "./env.js";
import type {
  Banner,
  GachaState,
  InventoryItem,
  MailItem,
  Wallet,
} from "./types.js";

export class SupabaseError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
  }
}

function serviceHeaders(env: Env): Record<string, string> {
  return {
    apikey: env.SUPABASE_SERVICE_ROLE_KEY,
    Authorization: `Bearer ${env.SUPABASE_SERVICE_ROLE_KEY}`,
    "Content-Type": "application/json",
  };
}

function anonHeaders(env: Env): Record<string, string> {
  return {
    apikey: env.SUPABASE_ANON_KEY,
    "Content-Type": "application/json",
  };
}

async function readError(res: Response): Promise<string> {
  try {
    const body = (await res.json()) as {
      message?: string;
      msg?: string;
      error_description?: string;
    };
    return body.message ?? body.msg ?? body.error_description ?? `HTTP ${res.status}`;
  } catch {
    return `HTTP ${res.status}`;
  }
}

/** Call a Postgres RPC (security definer). Returns the parsed JSON body. */
export async function rpc<T>(
  env: Env,
  fn: string,
  args: Record<string, unknown>,
): Promise<T> {
  const res = await fetch(`${env.SUPABASE_URL}/rest/v1/rpc/${fn}`, {
    method: "POST",
    headers: serviceHeaders(env),
    body: JSON.stringify(args),
  });
  if (!res.ok) throw new SupabaseError(res.status, await readError(res));
  return (await res.json()) as T;
}

/** Select rows from a PostgREST table with the service role key. */
export async function selectRows<T>(
  env: Env,
  table: string,
  query: string,
): Promise<T[]> {
  const res = await fetch(`${env.SUPABASE_URL}/rest/v1/${table}?${query}`, {
    headers: serviceHeaders(env),
  });
  if (!res.ok) throw new SupabaseError(res.status, await readError(res));
  return (await res.json()) as T[];
}

/** Exact row count via PostgREST (Prefer: count=exact + Content-Range). */
export async function countRows(env: Env, table: string): Promise<number> {
  const res = await fetch(
    `${env.SUPABASE_URL}/rest/v1/${table}?select=id&limit=1`,
    {
      headers: {
        ...serviceHeaders(env),
        Prefer: "count=exact",
      },
    },
  );
  if (!res.ok) throw new SupabaseError(res.status, await readError(res));
  const range = res.headers.get("content-range"); // e.g. "0-0/42"
  const total = range?.split("/")[1];
  return total ? Number(total) : 0;
}

// --- Row shapes (snake_case, as stored in Postgres) ---

interface BannerRow {
  id: number;
  name: string;
  featured_five_star: number;
  standard_five_star: number[];
  four_star: number[];
  three_star: number[];
  five_star_pity: number;
  four_star_pity: number;
  cost_per_pull: number;
  daily_limit: number;
  active: boolean;
}

interface GachaStateRow {
  user_id: string;
  banner_id: number;
  total: number;
  since_five: number;
  since_four: number;
  featured_since: number;
  guaranteed: boolean;
  daily_count: number;
  daily_anchor: string | null;
}

interface WalletRow {
  user_id: string;
  premium: number;
  free: number;
  tickets: number;
}

interface InventoryRow {
  user_id: string;
  item_id: number;
  kind: string;
  count: number;
  level: number;
}

interface MailRow {
  id: string;
  user_id: string;
  title: string;
  body: string;
  attachments: { itemId: number; kind: string; count: number }[];
  read: boolean;
  claimed: boolean;
  created_at: string;
  expires_at: string | null;
}

interface CloudSaveRow {
  user_id: string;
  slot: number;
  data_json: string;
  updated_at: string;
}

export function toBanner(row: BannerRow): Banner {
  return {
    id: row.id,
    name: row.name,
    featuredFiveStar: row.featured_five_star,
    standardFiveStar: row.standard_five_star,
    fourStar: row.four_star,
    threeStar: row.three_star,
    fiveStarPity: row.five_star_pity,
    fourStarPity: row.four_star_pity,
    costPerPull: row.cost_per_pull,
    dailyLimit: row.daily_limit,
  };
}

export function toGachaState(row: GachaStateRow): GachaState {
  return {
    total: row.total,
    sinceFive: row.since_five,
    sinceFour: row.since_four,
    featuredSince: row.featured_since,
    guaranteed: row.guaranteed,
    dailyCount: row.daily_count,
    dailyAnchor: row.daily_anchor,
  };
}

export function toWallet(row: WalletRow): Wallet {
  return { premium: row.premium, free: row.free, tickets: row.tickets };
}

export function toInventoryItem(row: InventoryRow): InventoryItem {
  return {
    accountId: row.user_id,
    itemId: row.item_id,
    kind: row.kind,
    count: row.count,
    level: row.level,
  };
}

export function toMailItem(row: MailRow): MailItem {
  return {
    id: row.id,
    accountId: row.user_id,
    title: row.title,
    body: row.body,
    attachments: row.attachments ?? [],
    read: row.read,
    claimed: row.claimed,
    createdAt: row.created_at,
    expiresAt: row.expires_at,
  };
}

// --- Queries used by the routes ---

export async function listBanners(env: Env): Promise<Banner[]> {
  const rows = await selectRows<BannerRow>(
    env,
    "banners",
    "select=*&active=eq.true&order=id.asc",
  );
  return rows.map(toBanner);
}

export async function getBanner(env: Env, id: number): Promise<Banner | null> {
  const rows = await selectRows<BannerRow>(
    env,
    "banners",
    `select=*&id=eq.${id}&active=eq.true&limit=1`,
  );
  return rows.length > 0 ? toBanner(rows[0] as BannerRow) : null;
}

export async function getGachaState(
  env: Env,
  userId: string,
  bannerId: number,
): Promise<GachaState | null> {
  const rows = await selectRows<GachaStateRow>(
    env,
    "gacha_state",
    `select=*&user_id=eq.${userId}&banner_id=eq.${bannerId}&limit=1`,
  );
  return rows.length > 0 ? toGachaState(rows[0] as GachaStateRow) : null;
}

export async function getInventory(env: Env, userId: string): Promise<InventoryItem[]> {
  const rows = await selectRows<InventoryRow>(
    env,
    "inventory",
    `select=*&user_id=eq.${userId}&order=item_id.asc`,
  );
  return rows.map(toInventoryItem);
}

export async function getMail(env: Env, userId: string): Promise<MailItem[]> {
  const rows = await selectRows<MailRow>(
    env,
    "mail",
    `select=*&user_id=eq.${userId}&order=created_at.desc`,
  );
  return rows.map(toMailItem);
}

export async function getCloudSave(
  env: Env,
  userId: string,
  slot: number,
): Promise<{ dataJson: string; updatedAt: string } | null> {
  const rows = await selectRows<CloudSaveRow>(
    env,
    "cloud_saves",
    `select=*&user_id=eq.${userId}&slot=eq.${slot}&limit=1`,
  );
  const row = rows[0];
  return row ? { dataJson: row.data_json, updatedAt: row.updated_at } : null;
}

/** Get-or-create wallet via RPC (starter grant 0 / 1000 / 0, like the C# prototype). */
export async function ensureWallet(env: Env, userId: string): Promise<Wallet> {
  const row = await rpc<WalletRow>(env, "ensure_wallet", { p_user_id: userId });
  return toWallet(row);
}

// --- Supabase Auth (anon key) ---

export interface AuthUser {
  id: string;
  email: string;
}

export interface AuthSession {
  accessToken: string;
  expiresIn: number;
  user: AuthUser;
}

interface GoTrueError {
  error?: string;
  msg?: string;
  message?: string;
}

/** Sign up with Supabase Auth. Returns the new user, or an error message. */
export async function authSignUp(
  env: Env,
  email: string,
  password: string,
): Promise<{ ok: true; user: AuthUser; hasSession: boolean } | { ok: false; error: string }> {
  const res = await fetch(`${env.SUPABASE_URL}/auth/v1/signup`, {
    method: "POST",
    headers: anonHeaders(env),
    body: JSON.stringify({ email, password }),
  });
  const body = (await res.json()) as {
    user?: { id: string; email: string };
    access_token?: string;
    error?: string;
    msg?: string;
  } & GoTrueError;
  if (!res.ok) {
    return { ok: false, error: body.msg ?? body.message ?? "signup failed" };
  }
  if (!body.user) return { ok: false, error: "signup failed" };
  return {
    ok: true,
    user: { id: body.user.id, email: body.user.email },
    // With email confirmation ON, signup returns a user but no session.
    hasSession: typeof body.access_token === "string",
  };
}

/** Sign in with Supabase Auth (password grant). */
export async function authSignIn(
  env: Env,
  email: string,
  password: string,
): Promise<{ ok: true; session: AuthSession } | { ok: false; error: string }> {
  const res = await fetch(`${env.SUPABASE_URL}/auth/v1/token?grant_type=password`, {
    method: "POST",
    headers: anonHeaders(env),
    body: JSON.stringify({ email, password }),
  });
  const body = (await res.json()) as {
    access_token?: string;
    expires_in?: number;
    user?: { id: string; email: string };
    error?: string;
    error_description?: string;
  };
  if (!res.ok || !body.access_token || !body.user) {
    return { ok: false, error: body.error_description ?? body.error ?? "invalid credentials" };
  }
  return {
    ok: true,
    session: {
      accessToken: body.access_token,
      expiresIn: body.expires_in ?? 3600,
      user: { id: body.user.id, email: body.user.email },
    },
  };
}

/** Create the public profile row for a new auth user (service role). */
export async function createProfile(
  env: Env,
  userId: string,
  username: string,
): Promise<void> {
  const res = await fetch(`${env.SUPABASE_URL}/rest/v1/profiles`, {
    method: "POST",
    headers: {
      ...serviceHeaders(env),
      Prefer: "resolution=merge-duplicates",
    },
    body: JSON.stringify({ id: userId, username }),
  });
  if (!res.ok) throw new SupabaseError(res.status, await readError(res));
}

/** Best-effort last_login_at update; never fails the login. */
export async function touchLastLogin(env: Env, userId: string): Promise<void> {
  try {
    await fetch(`${env.SUPABASE_URL}/rest/v1/profiles?id=eq.${userId}`, {
      method: "PATCH",
      headers: serviceHeaders(env),
      body: JSON.stringify({ last_login_at: new Date().toISOString() }),
    });
  } catch {
    // Ignore — login must not fail on a bookkeeping write.
  }
}
