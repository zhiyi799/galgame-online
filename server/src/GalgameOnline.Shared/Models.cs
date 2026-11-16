// Shared domain models for GalgameOnline.
// Clean-room design inspired by common gacha-game patterns (not copied from any
// specific codebase). All services reference this library.

namespace GalgameOnline.Shared;

/// <summary>An account on the SdkServer.</summary>
public sealed class Account
{
    public required Guid Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}

/// <summary>A login session token issued by the SdkServer (1 hour, like Lunaria).</summary>
public sealed class Session
{
    public required string Token { get; set; }
    public required Guid AccountId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>A gacha banner (pool). Configured server-side, not from a game dump.</summary>
public sealed class Banner
{
    public required uint Id { get; set; }
    public required string Name { get; set; }
    /// <summary>Featured 5-star item id (character or card).</summary>
    public required uint FeaturedFiveStar { get; set; }
    public required uint[] StandardFiveStar { get; set; }
    public required uint[] FourStar { get; set; }
    public required uint[] ThreeStar { get; set; }
    /// <summary>Hard pity for 5-star (0 = none). Typical 90.</summary>
    public uint FiveStarPity { get; set; } = 90;
    /// <summary>Hard pity for 4-star (0 = none). Typical 10.</summary>
    public uint FourStarPity { get; set; } = 10;
    /// <summary>Cost per single pull, in premium currency.</summary>
    public uint CostPerPull { get; set; } = 160;
    /// <summary>Daily pull cap (0 = unlimited).</summary>
    public uint DailyLimit { get; set; }
}

/// <summary>Per-account, per-banner gacha state (pity counters, guarantee).</summary>
public sealed class GachaState
{
    public required Guid AccountId { get; set; }
    public required uint BannerId { get; set; }
    public uint Total { get; set; }
    public uint SinceFive { get; set; }
    public uint SinceFour { get; set; }
    public uint FeaturedSince { get; set; }
    public bool Guaranteed { get; set; }
    public uint DailyCount { get; set; }
    public DateTimeOffset DailyAnchor { get; set; }
}

/// <summary>One pull result.</summary>
public sealed record PullOutcome(uint ItemId, int Rarity, bool IsFeatured, string Kind);

/// <summary>A wallet: premium currency (paid) + free currency + tickets.</summary>
public sealed class Wallet
{
    public required Guid AccountId { get; set; }
    public long Premium { get; set; }
    public long Free { get; set; }
    public long Tickets { get; set; }
}

/// <summary>One inventory entry: an owned character/card/item and its count.</summary>
public sealed class InventoryItem
{
    public required Guid AccountId { get; set; }
    public required uint ItemId { get; set; }
    public required string Kind { get; set; }
    public int Count { get; set; }
    public int Level { get; set; } = 1;
}

/// <summary>A mail message with optional attachments.</summary>
public sealed class MailItem
{
    public required Guid Id { get; set; }
    public required Guid AccountId { get; set; }
    public required string Title { get; set; }
    public required string Body { get; set; }
    public List<MailAttachment> Attachments { get; set; } = [];
    public bool Read { get; set; }
    public bool Claimed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed record MailAttachment(uint ItemId, string Kind, int Count);

/// <summary>Cloud save: one named slot per account, storing the game's save blob.</summary>
public sealed class CloudSave
{
    public required Guid AccountId { get; set; }
    public required int Slot { get; set; }
    public required string DataJson { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
