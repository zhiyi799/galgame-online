// Tests for the gacha engine port (src/gacha.ts), mirroring the verified C#
// behavior: hard pity 90/10, 50/50 guarantee, 120-pull featured guarantee,
// soft-pity ramp, daily reset, premium-first spend.

import { describe, expect, it } from "vitest";
import {
  bannerInfo,
  fiveStarRate,
  freshState,
  pull,
  resetDailyIfNeeded,
  splitCost,
} from "../src/gacha.js";
import type { Banner } from "../src/types.js";

const BANNER: Banner = {
  id: 1,
  name: "星夜邂逅",
  featuredFiveStar: 101,
  standardFiveStar: [102, 103, 104],
  fourStar: [201, 202, 203, 204],
  threeStar: [301, 302, 303],
  fiveStarPity: 90,
  fourStarPity: 10,
  costPerPull: 160,
  dailyLimit: 0,
};

/** RNG that never triggers a rate roll (0.999999) — only pity can hit. */
const never = () => 0.999999;
/** RNG that always triggers a rate roll and picks the first pool entry. */
const always = () => 0;

/** Deterministic RNG from a fixed sequence (repeats the last value). */
function sequence(values: number[]): () => number {
  let i = 0;
  const calls: number[] = [];
  const fn = () => {
    const v = values[Math.min(i, values.length - 1)] as number;
    calls.push(v);
    i++;
    return v;
  };
  (fn as unknown as { calls: number[] }).calls = calls;
  return fn;
}

describe("fiveStarRate (soft pity ramp)", () => {
  it("is the base rate before soft pity", () => {
    expect(fiveStarRate(0, 90)).toBe(0.008);
    expect(fiveStarRate(69, 90)).toBe(0.008);
  });
  it("ramps linearly from pull 70 to the hard pity", () => {
    expect(fiveStarRate(70, 90)).toBeCloseTo(0.008 + 0.992 * 0.05, 10);
    expect(fiveStarRate(80, 90)).toBeCloseTo(0.008 + 0.992 * 0.55, 10);
    expect(fiveStarRate(89, 90)).toBe(1.0);
  });
  it("is the base rate when there is no hard pity", () => {
    expect(fiveStarRate(100, 0)).toBe(0.008);
  });
});

describe("hard pity", () => {
  it("gives no 5-star in the first 89 pulls, then a 5-star on pull 90", () => {
    const now = new Date("2026-11-16T10:00:00Z");
    const { outcomes, state } = pull(BANNER, freshState(now), 89, now, never);
    expect(outcomes.filter((o) => o.rarity === 5)).toHaveLength(0);
    expect(state.sinceFive).toBe(89);

    const second = pull(BANNER, state, 1, now, never);
    expect(second.outcomes).toHaveLength(1);
    expect(second.outcomes[0]?.rarity).toBe(5);
    expect(second.state.sinceFive).toBe(0);
  });

  it("gives a 4-star on pull 10 and not before", () => {
    const now = new Date("2026-11-16T10:00:00Z");
    const first = pull(BANNER, freshState(now), 9, now, never);
    expect(first.outcomes.filter((o) => o.rarity === 4)).toHaveLength(0);
    const tenth = pull(BANNER, first.state, 1, now, never);
    expect(tenth.outcomes[0]?.rarity).toBe(4);
    expect(tenth.state.sinceFour).toBe(0);
  });

  it("resets the 4-star counter when a 5-star hits", () => {
    const now = new Date("2026-11-16T10:00:00Z");
    const state = freshState(now);
    state.sinceFour = 9;
    state.sinceFive = 89;
    const { outcomes, state: next } = pull(BANNER, state, 1, now, never);
    expect(outcomes[0]?.rarity).toBe(5);
    expect(next.sinceFour).toBe(0);
  });
});

describe("50/50 and featured guarantee", () => {
  it("losing the 50/50 sets Guaranteed, and the next 5-star is featured", () => {
    const now = new Date("2026-11-16T10:00:00Z");
    // Call 1: 5-star rate roll (0 < rate). Call 2: 50/50 roll (0.9 loses).
    // Call 3: pool pick for the standard 5-star.
    const rng1 = sequence([0, 0.9, 0.9]);
    const first = pull(BANNER, freshState(now), 1, now, rng1);
    expect(first.outcomes[0]?.rarity).toBe(5);
    expect(first.outcomes[0]?.isFeatured).toBe(false);
    expect(first.state.guaranteed).toBe(true);
    expect(first.state.featuredSince).toBe(1);

    // Guaranteed 5-star: only the rate roll is consumed (no 50/50 roll).
    const rng2 = sequence([0]);
    const second = pull(BANNER, first.state, 1, now, rng2);
    expect(second.outcomes[0]?.rarity).toBe(5);
    expect(second.outcomes[0]?.isFeatured).toBe(true);
    expect(second.outcomes[0]?.itemId).toBe(101);
    expect(second.state.guaranteed).toBe(false);
    expect(second.state.featuredSince).toBe(0);
    expect((rng2 as unknown as { calls: number[] }).calls).toHaveLength(1);
  });

  it("forces the featured 5-star on the 120th pull since the last featured", () => {
    const now = new Date("2026-11-16T10:00:00Z");
    const state = freshState(now);
    state.featuredSince = 119;
    const { outcomes, state: next } = pull(BANNER, state, 1, now, never);
    expect(outcomes[0]?.rarity).toBe(5);
    expect(outcomes[0]?.isFeatured).toBe(true);
    expect(outcomes[0]?.itemId).toBe(101);
    expect(next.featuredSince).toBe(0);
  });

  it("winning the 50/50 clears the guarantee", () => {
    const now = new Date("2026-11-16T10:00:00Z");
    // Rate roll hits, 50/50 roll 0.1 wins.
    const rng = sequence([0, 0.1]);
    const { outcomes, state } = pull(BANNER, freshState(now), 1, now, rng);
    expect(outcomes[0]?.isFeatured).toBe(true);
    expect(state.guaranteed).toBe(false);
  });
});

describe("daily reset", () => {
  it("resets the daily counter on a new UTC day", () => {
    const state = freshState(new Date("2026-11-15T10:00:00Z"));
    state.dailyCount = 7;
    const next = resetDailyIfNeeded(state, new Date("2026-11-16T00:00:01Z"));
    expect(next.dailyCount).toBe(0);
  });

  it("keeps the daily counter within the same UTC day", () => {
    const state = freshState(new Date("2026-11-16T10:00:00Z"));
    state.dailyCount = 7;
    const next = resetDailyIfNeeded(state, new Date("2026-11-16T23:59:00Z"));
    expect(next.dailyCount).toBe(7);
  });
});

describe("bannerInfo", () => {
  it("reports pity remaining and totals", () => {
    const state = freshState(new Date());
    state.total = 12;
    state.sinceFive = 89;
    state.sinceFour = 9;
    const info = bannerInfo(BANNER, state);
    expect(info).toEqual({
      bannerId: 1,
      totalCount: 12,
      fiveRemain: 1,
      fourRemain: 1,
      dailyLeft: 0, // unlimited banner reports 0 (C# quirk, kept)
    });
  });

  it("handles a fresh account (no state)", () => {
    expect(bannerInfo(BANNER, null)).toEqual({
      bannerId: 1,
      totalCount: 0,
      fiveRemain: 90,
      fourRemain: 10,
      dailyLeft: 0,
    });
  });
});

describe("splitCost (premium first, then free)", () => {
  it("spends premium first", () => {
    expect(splitCost(100, 1000, 160)).toEqual({ fromPremium: 100, fromFree: 60 });
  });
  it("spends only free when there is no premium", () => {
    expect(splitCost(0, 1000, 1600)).toEqual({ fromPremium: 0, fromFree: 1600 });
  });
  it("spends only premium when it covers the cost", () => {
    expect(splitCost(500, 0, 160)).toEqual({ fromPremium: 160, fromFree: 0 });
  });
});

describe("10-pull shape", () => {
  it("returns 10 outcomes and advances the counters by 10", () => {
    const now = new Date("2026-11-16T10:00:00Z");
    const { outcomes, state } = pull(BANNER, freshState(now), 10, now, always);
    expect(outcomes).toHaveLength(10);
    expect(state.total).toBe(10);
    expect(state.dailyCount).toBe(10);
  });
});
