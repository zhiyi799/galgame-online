// Game logic: wallet, gacha pulls, inventory, mail, cloud saves.
// All mutating operations run under the per-account lock so concurrent
// requests for one account cannot double-spend or double-claim.

using GalgameOnline.GameServer.Data;
using GalgameOnline.Shared;
using Microsoft.EntityFrameworkCore;

namespace GalgameOnline.GameServer.Services;

public sealed class GameService(GameDbContext db, AccountLocks locks)
{
    // --- Wallet ---

    public async Task<Wallet> GetOrCreateWalletAsync(Guid accountId)
    {
        var wallet = await db.Wallets.FirstOrDefaultAsync(w => w.AccountId == accountId);
        if (wallet is not null)
            return wallet;

        // Starter grant so a fresh account can pull immediately.
        wallet = new Wallet { AccountId = accountId, Premium = 0, Free = 1000, Tickets = 0 };
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();
        return wallet;
    }

    // --- Gacha ---

    public static GachaBannerInfo BannerInfo(Banner banner, GachaState? state) => new(
        banner.Id,
        state?.Total ?? 0,
        banner.FiveStarPity == 0 ? 0 : (uint)Math.Max(0, (int)banner.FiveStarPity - (int)(state?.SinceFive ?? 0)),
        banner.FourStarPity == 0 ? 0 : (uint)Math.Max(0, (int)banner.FourStarPity - (int)(state?.SinceFour ?? 0)),
        banner.DailyLimit == 0 ? 0 : (uint)Math.Max(0, (int)banner.DailyLimit - (int)(state?.DailyCount ?? 0)));

    public async Task<GachaState?> GetStateAsync(Guid accountId, uint bannerId) =>
        await db.GachaStates.FirstOrDefaultAsync(s => s.AccountId == accountId && s.BannerId == bannerId);

    public async Task<GachaPullResponse> PullAsync(Guid accountId, uint bannerId, int count)
    {
        if (count is not (1 or 10))
            return new(false, "count must be 1 or 10", null, null);

        var banner = BannerCatalog.Get(bannerId);
        if (banner is null)
            return new(false, $"unknown banner {bannerId}", null, null);

        using var _ = await locks.AcquireAsync(accountId);
        var now = DateTimeOffset.UtcNow;

        var state = await GetOrCreateStateAsync(accountId, bannerId);
        ResetDailyIfNeeded(state, now);

        if (banner.DailyLimit > 0 && state.DailyCount + count > banner.DailyLimit)
            return new(false, "daily pull limit reached", null, BannerInfo(banner, state));

        var wallet = await GetOrCreateWalletAsync(accountId);
        var cost = (long)banner.CostPerPull * count;
        var total = wallet.Premium + wallet.Free;
        if (total < cost)
            return new(false, $"insufficient currency: need {cost}, have {total}", null, BannerInfo(banner, state));

        // Combined spend: premium first, then free.
        var fromPremium = Math.Min(wallet.Premium, cost);
        wallet.Premium -= fromPremium;
        wallet.Free -= cost - fromPremium;

        var outcomes = GachaEngine.Pull(banner, state, count, now, Random.Shared);
        foreach (var outcome in outcomes)
            await AddToInventoryAsync(accountId, outcome.ItemId, outcome.Kind, 1);

        await db.SaveChangesAsync();
        return new(true, null, outcomes.ToList(), BannerInfo(banner, state));
    }

    // --- Inventory ---

    public async Task<List<InventoryItem>> GetInventoryAsync(Guid accountId) =>
        await db.InventoryItems
            .Where(i => i.AccountId == accountId)
            .OrderBy(i => i.ItemId)
            .ToListAsync();

    // --- Mail ---

    public async Task<List<MailItem>> GetMailAsync(Guid accountId)
    {
        // SQLite cannot ORDER BY DateTimeOffset; sort newest-first in memory.
        var mail = await db.MailItems
            .Where(m => m.AccountId == accountId)
            .ToListAsync();
        mail.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
        return mail;
    }

    public async Task<MailClaimResponse> ClaimMailAsync(Guid accountId, Guid mailId)
    {
        using var _ = await locks.AcquireAsync(accountId);

        var mail = await db.MailItems.FirstOrDefaultAsync(m => m.Id == mailId);
        if (mail is null)
            return new(false, "mail not found", null);
        if (mail.AccountId != accountId)
            return new(false, "mail does not belong to this account", null);
        if (mail.Claimed)
            return new(false, "mail already claimed", null);

        mail.Claimed = true;
        mail.Read = true;
        foreach (var att in mail.Attachments)
            await GrantAttachmentAsync(accountId, att);

        await db.SaveChangesAsync();
        return new(true, null, mail.Attachments);
    }

    public async Task<MailItem> SendMailAsync(Guid accountId, string title, string body, List<MailAttachment> attachments)
    {
        var mail = new MailItem
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Title = title,
            Body = body,
            Attachments = attachments,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.MailItems.Add(mail);
        await db.SaveChangesAsync();
        return mail;
    }

    // --- Cloud saves ---

    public async Task<bool> UploadSaveAsync(Guid accountId, int slot, string dataJson)
    {
        if (slot is < 1 or > 3)
            return false;

        var save = await db.CloudSaves.FirstOrDefaultAsync(s => s.AccountId == accountId && s.Slot == slot);
        if (save is null)
        {
            db.CloudSaves.Add(new CloudSave
            {
                AccountId = accountId,
                Slot = slot,
                DataJson = dataJson,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            save.DataJson = dataJson;
            save.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<SaveDownloadResponse> DownloadSaveAsync(Guid accountId, int slot)
    {
        var save = await db.CloudSaves.FirstOrDefaultAsync(s => s.AccountId == accountId && s.Slot == slot);
        return save is null
            ? new(false, "no save in this slot", null, null)
            : new(true, null, save.DataJson, save.UpdatedAt);
    }

    // --- Internals ---

    private async Task<GachaState> GetOrCreateStateAsync(Guid accountId, uint bannerId)
    {
        var state = await db.GachaStates
            .FirstOrDefaultAsync(s => s.AccountId == accountId && s.BannerId == bannerId);
        if (state is not null)
            return state;

        state = new GachaState
        {
            AccountId = accountId,
            BannerId = bannerId,
            DailyAnchor = DateTimeOffset.UtcNow,
        };
        db.GachaStates.Add(state);
        return state;
    }

    /// <summary>Same daily-reset rule as the engine, applied before the limit check.</summary>
    private static void ResetDailyIfNeeded(GachaState state, DateTimeOffset now)
    {
        if (state.DailyAnchor == default)
        {
            state.DailyAnchor = now;
        }
        else if (now.UtcDateTime.Date > state.DailyAnchor.UtcDateTime.Date)
        {
            state.DailyCount = 0;
            state.DailyAnchor = now;
        }
    }

    private async Task AddToInventoryAsync(Guid accountId, uint itemId, string kind, int count)
    {
        // Check the change tracker first: an item added earlier in this same
        // request (e.g. a duplicate pull in one 10-pull) is not in the DB yet,
        // so a store query would miss it and Add() would conflict.
        var item = db.InventoryItems.Local.FirstOrDefault(i => i.AccountId == accountId && i.ItemId == itemId)
            ?? await db.InventoryItems.FirstOrDefaultAsync(i => i.AccountId == accountId && i.ItemId == itemId);
        if (item is null)
        {
            db.InventoryItems.Add(new InventoryItem
            {
                AccountId = accountId,
                ItemId = itemId,
                Kind = kind,
                Count = count,
            });
        }
        else
        {
            item.Count += count;
        }
    }

    private async Task GrantAttachmentAsync(Guid accountId, MailAttachment att)
    {
        switch (att.Kind.ToLowerInvariant())
        {
            case "premium":
            case "free":
            case "ticket":
            {
                var wallet = await GetOrCreateWalletAsync(accountId);
                if (att.Kind.Equals("premium", StringComparison.OrdinalIgnoreCase))
                    wallet.Premium += att.Count;
                else if (att.Kind.Equals("free", StringComparison.OrdinalIgnoreCase))
                    wallet.Free += att.Count;
                else
                    wallet.Tickets += att.Count;
                break;
            }
            default:
                await AddToInventoryAsync(accountId, att.ItemId, att.Kind, att.Count);
                break;
        }
    }
}
