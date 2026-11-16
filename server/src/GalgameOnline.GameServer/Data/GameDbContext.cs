// EF Core SQLite store for the GameServer.
// Entities are the Shared domain models; keys and the mail-attachment JSON
// column are configured here. Accounts/sessions live on the SdkServer.

using GalgameOnline.Shared;
using Microsoft.EntityFrameworkCore;

namespace GalgameOnline.GameServer.Data;

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<GachaState> GachaStates => Set<GachaState>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<MailItem> MailItems => Set<MailItem>();
    public DbSet<CloudSave> CloudSaves => Set<CloudSave>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<GachaState>().HasKey(e => new { e.AccountId, e.BannerId });
        b.Entity<Wallet>().HasKey(e => e.AccountId);
        b.Entity<InventoryItem>().HasKey(e => new { e.AccountId, e.ItemId });
        b.Entity<MailItem>().HasKey(e => e.Id);
        // Attachments are a small value list; store them as a JSON column.
        b.Entity<MailItem>().OwnsMany(e => e.Attachments, a => a.ToJson());
        b.Entity<CloudSave>().HasKey(e => new { e.AccountId, e.Slot });
    }
}
