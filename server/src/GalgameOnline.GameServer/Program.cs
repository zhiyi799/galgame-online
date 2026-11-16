// GalgameOnline GameServer: gacha, wallet, inventory, mail, cloud saves.
// Plain REST + JSON for the HTML/JS galgame client. Auth is a Bearer session
// token validated against the SdkServer (GET /api/sdk/session).

using System.Net.Sockets;
using GalgameOnline.GameServer;
using GalgameOnline.GameServer.Data;
using GalgameOnline.GameServer.Services;
using GalgameOnline.Shared;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var port = builder.Configuration.GetValue<int>("GameServer:Port", 5100);
var sdkServerUrl = builder.Configuration.GetValue<string>("GameServer:SdkServerUrl") ?? "http://localhost:5000";
var sqlitePath = builder.Configuration.GetValue<string>("GameServer:Database:SqlitePath") ?? "data/game.db";
if (!Path.IsPathRooted(sqlitePath))
    sqlitePath = Path.Combine(builder.Environment.ContentRootPath, sqlitePath);

builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddDbContext<GameDbContext>(o => o.UseSqlite($"Data Source={sqlitePath}"));
// SdkServerUrl may be http(s)://... or unix:///path/to.sock (Unix-socket IPC,
// useful for localhost deployments and sandboxes that block loopback TCP).
if (sdkServerUrl.StartsWith("unix://", StringComparison.OrdinalIgnoreCase))
{
    var socketPath = sdkServerUrl["unix://".Length..];
    builder.Services.AddHttpClient<SdkSessionClient>((_, client) => client.BaseAddress = new Uri("http://localhost"))
        .ConfigurePrimaryHttpMessageHandler(() => UnixSocketHandler(socketPath));
}
else
{
    builder.Services.AddHttpClient<SdkSessionClient>((_, client) => client.BaseAddress = new Uri(sdkServerUrl));
}

static SocketsHttpHandler UnixSocketHandler(string socketPath)
{
    var handler = new SocketsHttpHandler();
    handler.ConnectCallback = async (_, ct) =>
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);
        return new NetworkStream(socket, ownsSocket: true);
    };
    return handler;
}
builder.Services.AddSingleton<AccountLocks>();
builder.Services.AddScoped<GameService>();

var app = builder.Build();

// Create the SQLite file/tables on first start.
var dbDir = Path.GetDirectoryName(sqlitePath);
if (!string.IsNullOrEmpty(dbDir))
    Directory.CreateDirectory(dbDir);
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    db.Database.EnsureCreated();
}

// --- Public ---

app.MapGet("/api/game/status", () => Results.Ok(new { ok = true }));

// --- Banners (auth required) ---

app.MapGet("/api/game/banners", async (HttpRequest req, SdkSessionClient sdk) =>
{
    var (_, err) = await AuthHelper.AuthenticateAsync(req, sdk);
    if (err is not null) return err;
    return Results.Ok(new BannerListResponse(
        BannerCatalog.All
            .Select(b => new BannerSummary(b.Id, b.Name, b.FeaturedFiveStar, b.CostPerPull, b.DailyLimit))
            .ToList()));
});

app.MapGet("/api/game/banner/{id:int}", async (int id, HttpRequest req, SdkSessionClient sdk, GameService svc) =>
{
    var (accountId, err) = await AuthHelper.AuthenticateAsync(req, sdk);
    if (err is not null) return err;
    var banner = BannerCatalog.Get((uint)id);
    if (banner is null)
        return Results.NotFound(new { error = $"unknown banner {id}" });
    var state = await svc.GetStateAsync(accountId!.Value, banner.Id);
    return Results.Ok(GameService.BannerInfo(banner, state));
});

// --- Gacha ---

app.MapPost("/api/game/gacha/pull", async (GachaPullRequest body, HttpRequest req, SdkSessionClient sdk, GameService svc) =>
{
    var (accountId, err) = await AuthHelper.AuthenticateAsync(req, sdk);
    if (err is not null) return err;
    var res = await svc.PullAsync(accountId!.Value, body.BannerId, body.Count);
    return Results.Ok(res);
});

// --- Wallet / inventory ---

app.MapGet("/api/game/wallet", async (HttpRequest req, SdkSessionClient sdk, GameService svc) =>
{
    var (accountId, err) = await AuthHelper.AuthenticateAsync(req, sdk);
    if (err is not null) return err;
    var w = await svc.GetOrCreateWalletAsync(accountId!.Value);
    return Results.Ok(new WalletResponse(w.Premium, w.Free, w.Tickets));
});

app.MapGet("/api/game/inventory", async (HttpRequest req, SdkSessionClient sdk, GameService svc) =>
{
    var (accountId, err) = await AuthHelper.AuthenticateAsync(req, sdk);
    if (err is not null) return err;
    var items = await svc.GetInventoryAsync(accountId!.Value);
    return Results.Ok(new InventoryResponse(items));
});

// --- Mail ---

app.MapGet("/api/game/mail", async (HttpRequest req, SdkSessionClient sdk, GameService svc) =>
{
    var (accountId, err) = await AuthHelper.AuthenticateAsync(req, sdk);
    if (err is not null) return err;
    var mail = await svc.GetMailAsync(accountId!.Value);
    return Results.Ok(new MailListResponse(mail));
});

app.MapPost("/api/game/mail/{id:guid}/claim", async (Guid id, HttpRequest req, SdkSessionClient sdk, GameService svc) =>
{
    var (accountId, err) = await AuthHelper.AuthenticateAsync(req, sdk);
    if (err is not null) return err;
    var res = await svc.ClaimMailAsync(accountId!.Value, id);
    return Results.Ok(res);
});

// Dev/admin helper: send a mail (with attachments) to the caller's own account.
app.MapPost("/api/game/mail/send", async (SendMailRequest body, HttpRequest req, SdkSessionClient sdk, GameService svc) =>
{
    var (accountId, err) = await AuthHelper.AuthenticateAsync(req, sdk);
    if (err is not null) return err;
    var mail = await svc.SendMailAsync(accountId!.Value, body.Title, body.Body, body.Attachments ?? []);
    return Results.Ok(new { ok = true, mailId = mail.Id });
});

// --- Cloud saves ---

app.MapPost("/api/game/save", async (SaveUploadRequest body, HttpRequest req, SdkSessionClient sdk, GameService svc) =>
{
    var (accountId, err) = await AuthHelper.AuthenticateAsync(req, sdk);
    if (err is not null) return err;
    if (body.Slot is < 1 or > 3)
        return Results.BadRequest(new { ok = false, error = "slot must be 1-3" });
    await svc.UploadSaveAsync(accountId!.Value, body.Slot, body.DataJson);
    return Results.Ok(new { ok = true });
});

app.MapGet("/api/game/save/{slot:int}", async (int slot, HttpRequest req, SdkSessionClient sdk, GameService svc) =>
{
    var (accountId, err) = await AuthHelper.AuthenticateAsync(req, sdk);
    if (err is not null) return err;
    var res = await svc.DownloadSaveAsync(accountId!.Value, slot);
    return Results.Ok(res);
});

app.Run();

/// <summary>Dev-helper request body for POST /api/game/mail/send.</summary>
public sealed record SendMailRequest(string Title, string Body, List<MailAttachment>? Attachments);
