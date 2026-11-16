// GachaRules — server-verified gacha constants and banner pool tables.
//
// WARNING: these values are COPIED from the server code and are NOT fetched
// from any API (there is no endpoint that returns pools or rates):
//   - rates / pity / guarantee: cloud/workers/src/gacha.ts
//     (BASE_FIVE_STAR_RATE, SOFT_PITY_START, BASE_FOUR_STAR_RATE,
//      RATE_UP_SHARE, FEATURED_GUARANTEE_PULLS)
//   - pool tables / pity limits / cost: cloud/supabase/migrations/0004_seed.sql
//     (banners table seed rows)
// If the server seed changes, these constants MUST be manually re-synced
// here, or the details/history UI will disagree with actual pulls.
// The server is always authoritative for pull outcomes; this file is
// display-only data for the client UI.
using System;

namespace GalgameOnline.Api
{
    public static class GachaRules
    {
        // --- Rates (gacha.ts) ---
        /// <summary>5-star base rate: 0.8%.</summary>
        public const double FiveStarBaseRate = 0.008;
        /// <summary>4-star base rate: 5%.</summary>
        public const double FourStarBaseRate = 0.05;
        /// <summary>5-star soft pity starts at this many pulls since the last 5-star (70).</summary>
        public const int SoftPityStart = 70;
        /// <summary>5-star hard pity: a 5-star is guaranteed within this many pulls (90).</summary>
        public const int FiveStarHardPity = 90;
        /// <summary>4-star hard pity: a 4-star or better is guaranteed within this many pulls (10).</summary>
        public const int FourStarHardPity = 10;
        /// <summary>Featured share on a 5-star roll: 50% featured, 50% standard pool (50/50).</summary>
        public const double FeaturedShare = 0.5;
        /// <summary>Featured guarantee: a featured 5-star is guaranteed within this many pulls since the last featured 5-star (120).</summary>
        public const int FeaturedGuaranteePulls = 120;

        // --- Cost (0004_seed.sql: cost_per_pull = 160) ---
        /// <summary>Cost of a single pull: 160 (premium first, then free).</summary>
        public const int CostPerPull = 160;
        /// <summary>Cost of a ten-pull: 1600.</summary>
        public const int TenPullCost = 1600;

        // --- Banner pool tables (0004_seed.sql) ---
        // Banner 1 （星夜邂逅）: featured 5★ 101; standard 5★ [102,103,104];
        //   4★ [201,202,203,204]; 3★ [301,302,303].
        // Banner 2 （月下初见）: featured 5★ 105; standard 5★ [102,103,104];
        //   4★ [201,202,203,205]; 3★ [301,302,304].
        // Banners 3-4 do NOT exist on the server (display-only coming-soon
        // slots) — no pool data is defined for them; the getters below
        // return -1 / empty arrays for any unknown banner id.

        private static readonly int[] Banner1StandardFiveStar = { 102, 103, 104 };
        private static readonly int[] Banner1FourStar = { 201, 202, 203, 204 };
        private static readonly int[] Banner1ThreeStar = { 301, 302, 303 };

        private static readonly int[] Banner2StandardFiveStar = { 102, 103, 104 };
        private static readonly int[] Banner2FourStar = { 201, 202, 203, 205 };
        private static readonly int[] Banner2ThreeStar = { 301, 302, 304 };

        /// <summary>Featured 5-star item id for a banner, or -1 for an unknown banner id.</summary>
        public static int GetFeaturedFiveStar(int bannerId)
        {
            switch (bannerId)
            {
                case 1: return 101;
                case 2: return 105;
                default: return -1;
            }
        }

        /// <summary>Standard (non-featured) 5-star pool for a banner. Empty array for an unknown banner id.</summary>
        public static int[] GetStandardFiveStarPool(int bannerId)
        {
            switch (bannerId)
            {
                case 1: return (int[])Banner1StandardFiveStar.Clone();
                case 2: return (int[])Banner2StandardFiveStar.Clone();
                default: return Array.Empty<int>();
            }
        }

        /// <summary>4-star pool for a banner. Empty array for an unknown banner id.</summary>
        public static int[] GetFourStarPool(int bannerId)
        {
            switch (bannerId)
            {
                case 1: return (int[])Banner1FourStar.Clone();
                case 2: return (int[])Banner2FourStar.Clone();
                default: return Array.Empty<int>();
            }
        }

        /// <summary>3-star pool for a banner. Empty array for an unknown banner id.</summary>
        public static int[] GetThreeStarPool(int bannerId)
        {
            switch (bannerId)
            {
                case 1: return (int[])Banner1ThreeStar.Clone();
                case 2: return (int[])Banner2ThreeStar.Clone();
                default: return Array.Empty<int>();
            }
        }
    }
}
