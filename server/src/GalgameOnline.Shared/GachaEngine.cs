// Gacha engine: rates, soft pity, hard pity, 50/50 guarantee.
// Rates follow the common gacha-game pattern (0.8% base 5-star, 5% 4-star,
// soft pity from 70, hard pity 90/10, 50% featured share, 120-pull featured
// guarantee). Implemented clean-room; no code is copied from any other project.

namespace GalgameOnline.Shared;

public static class GachaEngine
{
    public const double BaseFiveStarRate = 0.008;
    public const int SoftPityStart = 70;
    public const double BaseFourStarRate = 0.05;
    public const double RateUpShare = 0.5;
    public const int FeaturedGuaranteePulls = 120;

    /// <summary>Roll <paramref name="count"/> pulls on a banner, mutating <paramref name="state"/>.</summary>
    public static IReadOnlyList<PullOutcome> Pull(Banner banner, GachaState state, int count, DateTimeOffset now, Random rng)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        if (HasReset(state.DailyAnchor, now))
        {
            state.DailyCount = 0;
            state.DailyAnchor = now;
        }
        else if (state.DailyAnchor == default)
        {
            state.DailyAnchor = now;
        }

        state.Total += (uint)count;
        state.DailyCount += (uint)count;

        var outcomes = new List<PullOutcome>(count);
        for (var i = 0; i < count; i++)
            outcomes.Add(PullOne(banner, state, rng));
        return outcomes;
    }

    private static PullOutcome PullOne(Banner banner, GachaState state, Random rng)
    {
        var pityFive = banner.FiveStarPity;
        var pityFour = banner.FourStarPity;

        var pityHit = pityFive > 0 && state.SinceFive + 1 >= pityFive;
        var featureDue = state.FeaturedSince + 1 >= FeaturedGuaranteePulls;
        var isFive = pityHit || featureDue || rng.NextDouble() < FiveStarRate(state.SinceFive, pityFive);

        if (isFive)
        {
            var featured = featureDue || state.Guaranteed || rng.NextDouble() < RateUpShare;
            var id = featured ? banner.FeaturedFiveStar : Pick(banner.StandardFiveStar, rng);
            state.SinceFive = 0;
            state.SinceFour = 0;
            state.FeaturedSince = featured ? 0 : state.FeaturedSince + 1;
            state.Guaranteed = !featured;
            return new PullOutcome(id, 5, featured, "character");
        }

        var fourPityHit = pityFour > 0 && state.SinceFour + 1 >= pityFour;
        var isFour = fourPityHit || rng.NextDouble() < BaseFourStarRate;

        if (isFour)
        {
            state.SinceFive++;
            state.SinceFour = 0;
            state.FeaturedSince++;
            return new PullOutcome(Pick(banner.FourStar, rng), 4, false, "character");
        }

        state.SinceFive++;
        state.SinceFour++;
        state.FeaturedSince++;
        return new PullOutcome(Pick(banner.ThreeStar, rng), 3, false, "item");
    }

    internal static double FiveStarRate(uint sinceFive, uint pityFive)
    {
        if (pityFive == 0 || sinceFive < SoftPityStart)
            return BaseFiveStarRate;

        var span = pityFive > SoftPityStart ? pityFive - SoftPityStart : 0;
        if (span == 0)
            return 1.0;

        var t = Math.Min((double)(sinceFive - SoftPityStart + 1) / span, 1.0);
        return BaseFiveStarRate + (1.0 - BaseFiveStarRate) * t;
    }

    private static uint Pick(uint[] pool, Random rng) =>
        pool.Length == 0 ? throw new InvalidOperationException("gacha banner pool is empty") : pool[rng.Next(pool.Length)];

    private static bool HasReset(DateTimeOffset anchor, DateTimeOffset now) =>
        now.UtcDateTime.Date > anchor.UtcDateTime.Date;
}
