using System.Text.RegularExpressions;
using GalgameOnline.SdkServer.Persistence;
using GalgameOnline.SdkServer.Services;
using GalgameOnline.Shared;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GalgameOnline.SdkServer;

/// <summary>All SdkServer HTTP endpoints, under /api/sdk/.</summary>
public static class SdkEndpoints
{
    private static readonly Regex UsernamePattern = new("^[A-Za-z0-9_]{3,32}$", RegexOptions.Compiled);

    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/sdk");

        // POST /api/sdk/signup — rate limited per IP.
        group.MapPost("/signup", SignupAsync)
            .RequireRateLimiting("auth");

        // POST /api/sdk/login — rate limited per IP.
        group.MapPost("/login", LoginAsync)
            .RequireRateLimiting("auth");

        // GET /api/sdk/session — Authorization: Bearer <token>.
        group.MapGet("/session", SessionCheckAsync);

        // GET /api/sdk/status — service health + counts.
        group.MapGet("/status", StatusAsync);
    }

    private static async Task<IResult> SignupAsync(
        SignupRequest req, SdkDbContext db, CancellationToken ct)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Username) || req.Password is null)
        {
            return Results.Json(new SignupResponse(false, "Username and password are required.", null),
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!UsernamePattern.IsMatch(req.Username))
        {
            return Results.Json(
                new SignupResponse(false, "Username must be 3-32 characters: letters, digits, underscore.", null),
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (req.Password.Length < 8)
        {
            return Results.Json(
                new SignupResponse(false, "Password must be at least 8 characters.", null),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var existing = await db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Username == req.Username, ct);
        if (existing is not null)
        {
            return Results.Json(
                new SignupResponse(false, "Username is already taken.", null),
                statusCode: StatusCodes.Status409Conflict);
        }

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Username = req.Username,
            PasswordHash = PasswordHasher.Hash(req.Password),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Accounts.Add(account);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Unique-index race: another request took the name first.
            return Results.Json(
                new SignupResponse(false, "Username is already taken.", null),
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Json(new SignupResponse(true, null, account.Id));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest req, SdkDbContext db, SessionService sessions,
        IOptions<SdkServerOptions> options, CancellationToken ct)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Username) || req.Password is null)
        {
            return Results.Json(new LoginResponse(false, "Username and password are required.", null, null, null),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var account = await db.Accounts
            .FirstOrDefaultAsync(a => a.Username == req.Username, ct);

        // Same generic error for unknown user and wrong password (no user enumeration).
        if (account is null || !PasswordHasher.Verify(account.PasswordHash, req.Password))
        {
            return Results.Json(new LoginResponse(false, "Invalid username or password.", null, null, null),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var session = await sessions.CreateAsync(account.Id, ct);

        account.LastLoginAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Json(new LoginResponse(
            true, null, session.Token, session.ExpiresAt, options.Value.GameServerUrl));
    }

    private static async Task<IResult> SessionCheckAsync(
        HttpContext http, SessionService sessions, CancellationToken ct)
    {
        var auth = http.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(auth) ||
            !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(new SessionCheckResponse(false, null));
        }

        var token = auth["Bearer ".Length..].Trim();
        if (token.Length == 0)
        {
            return Results.Json(new SessionCheckResponse(false, null));
        }

        var session = await sessions.FindLiveAsync(token, ct);
        if (session is null)
        {
            return Results.Json(new SessionCheckResponse(false, null));
        }

        return Results.Json(new SessionCheckResponse(true, session.AccountId));
    }

    private static async Task<IResult> StatusAsync(SdkDbContext db, CancellationToken ct)
    {
        var accounts = await db.Accounts.CountAsync(ct);
        var sessions = await db.Sessions.CountAsync(ct);
        return Results.Json(new { ok = true, accounts, sessions });
    }
}
