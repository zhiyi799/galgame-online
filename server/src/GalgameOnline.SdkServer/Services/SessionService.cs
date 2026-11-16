using System.Security.Cryptography;
using GalgameOnline.SdkServer.Persistence;
using GalgameOnline.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GalgameOnline.SdkServer.Services;

/// <summary>Issues and validates login session tokens.</summary>
public sealed class SessionService
{
    private readonly SdkDbContext _db;
    private readonly SdkServerOptions _options;

    public SessionService(SdkDbContext db, IOptions<SdkServerOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    /// <summary>Create a new session for an account. Token is 32 random bytes, base64url.</summary>
    public async Task<Session> CreateAsync(Guid accountId, CancellationToken ct = default)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var session = new Session
        {
            Token = token,
            AccountId = accountId,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(_options.TokenTtlSeconds),
        };

        _db.Sessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return session;
    }

    /// <summary>Look up a live (unexpired) session by token. Returns null if missing/expired.</summary>
    public async Task<Session?> FindLiveAsync(string token, CancellationToken ct = default)
    {
        var session = await _db.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Token == token, ct);

        if (session is null || session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        return session;
    }
}
