// Gacha engine — faithful TypeScript port of the C# prototype's GachaEngine.cs.
// Rates follow the common gacha-game pattern: 5-star base 0.8%, soft pity from
// 70 pulls ramping linearly, hard pity 90/10, 50% featured share, 120-pull
// featured guarantee. Clean-room; no code copied from any other project.
//
// The RNG is injected so tests can drive it deterministically. Production uses
// cryptoRandom() (WebCrypto). The draw order of RNG calls mirrors the C#
// engine exactly: 5-star rate roll, then (only on a 5-star that is not already
// forced featured) the 50/50 roll, then the 4-star rate roll.

import type { Banner, GachaState, PullOutcome } from "./types.js";

export const BASE_FIVE_STAR_RATE = 0.008;
export const SOFT_PITY_START = 70;
export const BASE_FOUR_STAR_RATE = 0.05;
export const RATE_UP_SHARE = 0.5;
export const FEATURED_GUARANTEE_PULLS = 120;

export type Rng = () => number;

/** Production RNG: uniform in [0, 1) from WebCrypto. */
export function cryptoRandom(): number {
  const buf = new Uint32Array(1);
  crypto.getRandomValues(buf);
  return (buf[0] as number) / 2 ** 32;
}

/** A fresh, never-pulled state for (account, banner). */
export function freshState(now: Date): GachaState {
  return {
    total: 0,
    sinceFive: 0,
    sinceFour: 0,
    featuredSince: 0,
    guaranteed: false,
    dailyCount: 0,
    dailyAnchor: now.toISOString(),
  };
}

/** UTC calendar day (YYYY-MM-DD) of a timestamp — the daily-reset boundary. */
export function utcDay(iso: string): string {
  return iso.slice(0, 10);
}

/**
 * Apply the C# engine's daily reset to a state copy: if `now` is a later UTC
 * day than the state's anchor, the daily counter resets to 0.
 */
export function resetDailyIfNeeded(state: GachaState, now: Date): GachaState {
  const next = { ...state };
  if (next.dailyAnchor === null) {
    next.dailyAnchor = now.toISOString();
  } else if (utcDay(now.toISOString()) > utcDay(next.dailyAnchor)) {
    next.dailyCount = 0;
    next.dailyAnchor = now.toISOString();
  }
  return next;
}

/** Roll `count` pulls on a banner, mutating and returning the new state. */
export function pull(
  banner: Banner,
  state: GachaState,
  count: number,
  now: Date,
  rng: Rng,
): { outcomes: PullOutcome[]; state: GachaState } {
  if (count < 1) throw new RangeError("count must be >= 1");
  const next = resetDailyIfNeeded(state, now);
  next.total += count;
  next.dailyCount += count;

  const outcomes: PullOutcome[] = [];
  for (let i = 0; i < count; i++) outcomes.push(pullOne(banner, next, rng));
  return { outcomes, state: next };
}

function pullOne(banner: Banner, state: GachaState, rng: Rng): PullOutcome {
  const pityFive = banner.fiveStarPity;
  const pityFour = banner.fourStarPity;

  const pityHit = pityFive > 0 && state.sinceFive + 1 >= pityFive;
  const featureDue = state.featuredSince + 1 >= FEATURED_GUARANTEE_PULLS;
  const isFive =
    pityHit || featureDue || rng() < fiveStarRate(state.sinceFive, pityFive);

  if (isFive) {
    // Short-circuit order mirrors C#: featureDue / guaranteed consume no RNG.
    const featured = featureDue || state.guaranteed || rng() < RATE_UP_SHARE;
    const id = featured
      ? banner.featuredFiveStar
      : pick(banner.standardFiveStar, rng);
    state.sinceFive = 0;
    state.sinceFour = 0;
    state.featuredSince = featured ? 0 : state.featuredSince + 1;
    state.guaranteed = !featured;
    return { itemId: id, rarity: 5, isFeatured: featured, kind: "character" };
  }

  const fourPityHit = pityFour > 0 && state.sinceFour + 1 >= pityFour;
  const isFour = fourPityHit || rng() < BASE_FOUR_STAR_RATE;

  if (isFour) {
    state.sinceFive++;
    state.sinceFour = 0;
    state.featuredSince++;
    return { itemId: pick(banner.fourStar, rng), rarity: 4, isFeatured: false, kind: "character" };
  }

  state.sinceFive++;
  state.sinceFour++;
  state.featuredSince++;
  return { itemId: pick(banner.threeStar, rng), rarity: 3, isFeatured: false, kind: "item" };
}

export function fiveStarRate(sinceFive: number, pityFive: number): number {
  if (pityFive === 0 || sinceFive < SOFT_PITY_START) return BASE_FIVE_STAR_RATE;

  const span = pityFive > SOFT_PITY_START ? pityFive - SOFT_PITY_START : 0;
  if (span === 0) return 1.0;

  const t = Math.min((sinceFive - SOFT_PITY_START + 1) / span, 1.0);
  return BASE_FIVE_STAR_RATE + (1.0 - BASE_FIVE_STAR_RATE) * t;
}

function pick(pool: number[], rng: Rng): number {
  if (pool.length === 0) throw new Error("gacha banner pool is empty");
  return pool[Math.floor(rng() * pool.length)] as number;
}

/** Pity info shown to the client (same shape/semantics as C# BannerInfo). */
export function bannerInfo(
  banner: Banner,
  state: GachaState | null,
): {
  bannerId: number;
  totalCount: number;
  fiveRemain: number;
  fourRemain: number;
  dailyLeft: number;
} {
  return {
    bannerId: banner.id,
    totalCount: state?.total ?? 0,
    fiveRemain:
      banner.fiveStarPity === 0
        ? 0
        : Math.max(0, banner.fiveStarPity - (state?.sinceFive ?? 0)),
    fourRemain:
      banner.fourStarPity === 0
        ? 0
        : Math.max(0, banner.fourStarPity - (state?.sinceFour ?? 0)),
    // Quirk inherited from the C# prototype: an unlimited banner (dailyLimit
    // 0) reports dailyLeft 0 rather than "infinite".
    dailyLeft:
      banner.dailyLimit === 0
        ? 0
        : Math.max(0, banner.dailyLimit - (state?.dailyCount ?? 0)),
  };
}

/**
 * Combined spend, premium first then free — mirrors C# PullAsync.
 * Returns how much comes out of each balance. Caller must check
 * premium + free >= cost first.
 */
export function splitCost(
  premium: number,
  free: number,
  cost: number,
): { fromPremium: number; fromFree: number } {
  const fromPremium = Math.min(premium, cost);
  return { fromPremium, fromFree: cost - fromPremium };
}
