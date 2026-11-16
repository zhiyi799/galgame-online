// API request/response DTOs (REST + JSON). All three services speak these.

namespace GalgameOnline.Shared;

// --- SdkServer ---
public sealed record SignupRequest(string Username, string Password);
public sealed record SignupResponse(bool Ok, string? Error, Guid? AccountId);
public sealed record LoginRequest(string Username, string Password);
public sealed record LoginResponse(bool Ok, string? Error, string? Token, DateTimeOffset? ExpiresAt, string? GameServerUrl);
public sealed record SessionCheckResponse(bool Ok, Guid? AccountId);

// --- GameServer ---
public sealed record GachaPullRequest(uint BannerId, int Count);
public sealed record GachaPullResponse(bool Ok, string? Error, List<PullOutcome>? Pulls, GachaBannerInfo? Banner);
public sealed record GachaBannerInfo(uint BannerId, uint TotalCount, uint FiveRemain, uint FourRemain, uint DailyLeft);
public sealed record BannerListResponse(List<BannerSummary> Banners);
public sealed record BannerSummary(uint Id, string Name, uint FeaturedFiveStar, uint CostPerPull, uint DailyLimit);
public sealed record WalletResponse(long Premium, long Free, long Tickets);
public sealed record InventoryResponse(List<InventoryItem> Items);
public sealed record MailListResponse(List<MailItem> Mail);
public sealed record MailClaimResponse(bool Ok, string? Error, List<MailAttachment>? Granted);
public sealed record SaveUploadRequest(int Slot, string DataJson);
public sealed record SaveDownloadResponse(bool Ok, string? Error, string? DataJson, DateTimeOffset? UpdatedAt);

// --- QueryGateway ---
public sealed record RegisterRequest(string InstanceId, string Url, int Port, int MaxPlayers);
public sealed record RegisterResponse(bool Ok, string? Error);
public sealed record HeartbeatRequest(string InstanceId, int Players);
public sealed record AllocateResponse(bool Ok, string? Error, string? Url);
public sealed record InstanceSummary(string InstanceId, string Url, int Players, int MaxPlayers, DateTimeOffset LastHeartbeat);
