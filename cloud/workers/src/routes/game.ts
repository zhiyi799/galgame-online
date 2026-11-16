// Game routes — /api/game/* (all authenticated except /status).
// Every mutating operation runs through a security-definer Postgres RPC so a
// pull, a mail claim, and a save write are each atomic. The Worker computes
// the gacha draw with the TypeScript engine (src/gacha.ts, ported from the
// C# prototype) and commits it with do_gacha_pull, which re-validates funds
// and the daily limit against row-locked data and rejects stale state
// ("state_changed") — the Worker retries the whole pull (max 3 attempts).
// That is the Workers equivalent of the C# prototype's per-account locks.

import type { Env } from "../env.js";
import {
  bannerInfo,
  cryptoRandom,
  freshState,
  pull,
  resetDailyIfNeeded,
  splitCost,
} from "../gacha.js";
import { err, json, parseJson, requireAuth } from "../http.js";
import {
  SupabaseError,
  ensureWallet,
  getBanner,
  getCloudSave,
  getGachaState,
  getInventory,
  getMail,
  listBanners,
  rpc,
} from "../supabase.js";
import type {
  GachaPullRequest,
  GachaState,
  MailAttachment,
  PullOutcome,
  SaveUploadRequest,
  SendMailRequest,
} from "../types.js";

const MAX_PULL_ATTEMPTS = 3;

function stateToJson(s: GachaState): Record<string, unknown> {
  return {
    total: s.total,
    since_five: s.sinceFive,
    since_four: s.sinceFour,
    featured_since: s.featuredSince,
    guaranteed: s.guaranteed,
    daily_count: s.dailyCount,
    daily_anchor: s.dailyAnchor,
  };
}

interface RpcResult {
  ok: boolean;
  error?: string;
  [key: string]: unknown;
}

async function doGachaPull(
  env: Env,
  args: Record<string, unknown>,
): Promise<RpcResult> {
  return rpc<RpcResult>(env, "do_gacha_pull", args);
}

export async function gameStatus(): Promise<Response> {
  return json({ ok: true });
}

export async function banners(request: Request, env: Env): Promise<Response> {
  const auth = await requireAuth(request, env);
  if ("response" in auth) return auth.response;
  try {
    const list = await listBanners(env);
    return json({
      banners: list.map((b) => ({
        id: b.id,
        name: b.name,
        featuredFiveStar: b.featuredFiveStar,
        costPerPull: b.costPerPull,
        dailyLimit: b.dailyLimit,
      })),
    });
  } catch (e) {
    if (e instanceof SupabaseError) return err("banners unavailable", 502);
    throw e;
  }
}

export async function bannerDetail(
  request: Request,
  env: Env,
  id: number,
): Promise<Response> {
  const auth = await requireAuth(request, env);
  if ("response" in auth) return auth.response;
  try {
    const banner = await getBanner(env, id);
    if (!banner) return json({ error: `unknown banner ${id}` }, 404);
    const state = await getGachaState(env, auth.userId, banner.id);
    return json(bannerInfo(banner, state));
  } catch (e) {
    if (e instanceof SupabaseError) return err("banner unavailable", 502);
    throw e;
  }
}

export async function gachaPull(request: Request, env: Env): Promise<Response> {
  const auth = await requireAuth(request, env);
  if ("response" in auth) return auth.response;

  const body = await parseJson<GachaPullRequest>(request);
  if (!body) return err("bannerId and count are required.", 400);
  if (body.count !== 1 && body.count !== 10) {
    return json({ ok: false, error: "count must be 1 or 10", pulls: null, banner: null });
  }

  try {
    const banner = await getBanner(env, body.bannerId);
    if (!banner) {
      return json({ ok: false, error: `unknown banner ${body.bannerId}`, pulls: null, banner: null });
    }

    const now = new Date();
    const cost = banner.costPerPull * body.count;

    for (let attempt = 1; attempt <= MAX_PULL_ATTEMPTS; attempt++) {
      const wallet = await ensureWallet(env, auth.userId);
      const stored = await getGachaState(env, auth.userId, banner.id);
      const base = resetDailyIfNeeded(stored ?? freshState(now), now);

      if (banner.dailyLimit > 0 && base.dailyCount + body.count > banner.dailyLimit) {
        return json({ ok: false, error: "daily pull limit reached", pulls: null, banner: bannerInfo(banner, base) });
      }
      const total = wallet.premium + wallet.free;
      if (total < cost) {
        return json({
          ok: false,
          error: `insufficient currency: need ${cost}, have ${total}`,
          pulls: null,
          banner: bannerInfo(banner, base),
        });
      }

      const { fromPremium, fromFree } = splitCost(wallet.premium, wallet.free, cost);
      const { outcomes, state: newState } = pull(banner, base, body.count, now, cryptoRandom);

      const res = await doGachaPull(env, {
        p_user_id: auth.userId,
        p_banner_id: banner.id,
        p_count: body.count,
        p_cost_premium: fromPremium,
        p_cost_free: fromFree,
        p_expected_state: stateToJson(base),
        p_new_state: stateToJson(newState),
        p_outcomes: outcomes.map((o: PullOutcome) => ({
          item_id: o.itemId,
          kind: o.kind,
          count: 1,
        })),
      });

      if (res.ok) {
        return json({ ok: true, error: null, pulls: outcomes, banner: bannerInfo(banner, newState) });
      }
      if (res.error === "state_changed" && attempt < MAX_PULL_ATTEMPTS) {
        continue; // Another pull for this account committed first; retry.
      }
      return json({ ok: false, error: res.error ?? "pull failed", pulls: null, banner: bannerInfo(banner, base) });
    }
    return json({ ok: false, error: "pull conflict, please retry", pulls: null, banner: null });
  } catch (e) {
    if (e instanceof SupabaseError) return err("pull unavailable", 502);
    throw e;
  }
}

export async function wallet(request: Request, env: Env): Promise<Response> {
  const auth = await requireAuth(request, env);
  if ("response" in auth) return auth.response;
  try {
    const w = await ensureWallet(env, auth.userId);
    return json({ premium: w.premium, free: w.free, tickets: w.tickets });
  } catch (e) {
    if (e instanceof SupabaseError) return err("wallet unavailable", 502);
    throw e;
  }
}

export async function inventory(request: Request, env: Env): Promise<Response> {
  const auth = await requireAuth(request, env);
  if ("response" in auth) return auth.response;
  try {
    const items = await getInventory(env, auth.userId);
    return json({ items });
  } catch (e) {
    if (e instanceof SupabaseError) return err("inventory unavailable", 502);
    throw e;
  }
}

export async function mail(request: Request, env: Env): Promise<Response> {
  const auth = await requireAuth(request, env);
  if ("response" in auth) return auth.response;
  try {
    const items = await getMail(env, auth.userId);
    return json({ mail: items });
  } catch (e) {
    if (e instanceof SupabaseError) return err("mail unavailable", 502);
    throw e;
  }
}

export async function claimMail(
  request: Request,
  env: Env,
  mailId: string,
): Promise<Response> {
  const auth = await requireAuth(request, env);
  if ("response" in auth) return auth.response;
  try {
    const res = await rpc<RpcResult>(env, "claim_mail", {
      p_user_id: auth.userId,
      p_mail_id: mailId,
    });
    if (!res.ok) return json({ ok: false, error: res.error ?? "claim failed", granted: null });
    return json({ ok: true, error: null, granted: (res.granted ?? []) as MailAttachment[] });
  } catch (e) {
    if (e instanceof SupabaseError) return err("claim unavailable", 502);
    throw e;
  }
}

export async function sendMail(request: Request, env: Env): Promise<Response> {
  // Dev/admin helper (mirrors the C# prototype's /mail/send). Disabled unless
  // ALLOW_DEV_MAIL_SEND=true — in production mail is sent by server jobs, not
  // by players.
  if (env.ALLOW_DEV_MAIL_SEND !== "true") {
    return err("mail send is disabled", 403);
  }
  const auth = await requireAuth(request, env);
  if ("response" in auth) return auth.response;
  const body = await parseJson<SendMailRequest>(request);
  if (!body || typeof body.title !== "string" || typeof body.body !== "string") {
    return err("title and body are required.", 400);
  }
  try {
    const res = await rpc<RpcResult>(env, "send_mail", {
      p_user_id: auth.userId,
      p_title: body.title,
      p_body: body.body,
      p_attachments: (body.attachments ?? []).map((a) => ({
        itemId: a.itemId,
        kind: a.kind,
        count: a.count,
      })),
    });
    if (!res.ok) return json({ ok: false, error: res.error ?? "send failed" });
    return json({ ok: true, mailId: res.mail_id as string });
  } catch (e) {
    if (e instanceof SupabaseError) return err("mail unavailable", 502);
    throw e;
  }
}

export async function uploadSave(request: Request, env: Env): Promise<Response> {
  const auth = await requireAuth(request, env);
  if ("response" in auth) return auth.response;
  const body = await parseJson<SaveUploadRequest>(request);
  if (!body || typeof body.dataJson !== "string") {
    return err("slot and dataJson are required.", 400);
  }
  if (body.slot < 1 || body.slot > 3) {
    return json({ ok: false, error: "slot must be 1-3" }, 400);
  }
  try {
    const res = await rpc<RpcResult>(env, "upsert_cloud_save", {
      p_user_id: auth.userId,
      p_slot: body.slot,
      p_data: body.dataJson,
    });
    if (!res.ok) return json({ ok: false, error: res.error ?? "save failed" });
    return json({ ok: true });
  } catch (e) {
    if (e instanceof SupabaseError) return err("save unavailable", 502);
    throw e;
  }
}

export async function downloadSave(
  request: Request,
  env: Env,
  slot: number,
): Promise<Response> {
  const auth = await requireAuth(request, env);
  if ("response" in auth) return auth.response;
  try {
    const save = await getCloudSave(env, auth.userId, slot);
    if (!save) return json({ ok: false, error: "no save in this slot", dataJson: null, updatedAt: null });
    return json({ ok: true, error: null, dataJson: save.dataJson, updatedAt: save.updatedAt });
  } catch (e) {
    if (e instanceof SupabaseError) return err("save unavailable", 502);
    throw e;
  }
}

/**
 * Compatibility route for the old QueryGateway: Workers are serverless, there
 * are no game-server instances to allocate. The Worker itself is the game
 * server, so allocation always returns this Worker's origin.
 */
export async function allocate(request: Request, _env: Env): Promise<Response> {
  return json({ ok: true, error: null, url: new URL(request.url).origin });
}
