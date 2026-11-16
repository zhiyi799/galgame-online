// Banner catalog: static, configured in code (not a DB table).
// Item ids are opaque to the server; the client maps them to characters/cards.

using GalgameOnline.Shared;

namespace GalgameOnline.GameServer.Services;

public static class BannerCatalog
{
    public static readonly IReadOnlyList<Banner> All =
    [
        new Banner
        {
            Id = 1,
            Name = "星夜邂逅",
            FeaturedFiveStar = 101,
            StandardFiveStar = [102, 103, 104],
            FourStar = [201, 202, 203, 204],
            ThreeStar = [301, 302, 303],
            FiveStarPity = 90,
            FourStarPity = 10,
            CostPerPull = 160,
            DailyLimit = 0,
        },
        new Banner
        {
            Id = 2,
            Name = "月下初见",
            FeaturedFiveStar = 105,
            StandardFiveStar = [102, 103, 104],
            FourStar = [201, 202, 203, 205],
            ThreeStar = [301, 302, 304],
            FiveStarPity = 90,
            FourStarPity = 10,
            CostPerPull = 160,
            DailyLimit = 0,
        },
    ];

    public static Banner? Get(uint id) => All.FirstOrDefault(b => b.Id == id);
}
