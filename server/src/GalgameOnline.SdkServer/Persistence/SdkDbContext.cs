using GalgameOnline.Shared;
using Microsoft.EntityFrameworkCore;

namespace GalgameOnline.SdkServer.Persistence;

/// <summary>EF Core context for the SdkServer. SQLite only; one file.</summary>
public sealed class SdkDbContext : DbContext
{
    public SdkDbContext(DbContextOptions<SdkDbContext> options) : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Session> Sessions => Set<Session>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.Username).IsUnique();
            entity.Property(a => a.Username).IsRequired().HasMaxLength(32);
            entity.Property(a => a.PasswordHash).IsRequired();
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(s => s.Token);
            entity.HasIndex(s => s.AccountId);
            entity.HasIndex(s => s.ExpiresAt);
        });
    }
}
