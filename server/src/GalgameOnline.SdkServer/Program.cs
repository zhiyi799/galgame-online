using System.Threading.RateLimiting;
using GalgameOnline.SdkServer;
using GalgameOnline.SdkServer.Persistence;
using GalgameOnline.SdkServer.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Config: SdkServer section of appsettings.json ---
var options = builder.Configuration.GetSection("SdkServer").Get<SdkServerOptions>()
              ?? new SdkServerOptions();
builder.Services.Configure<SdkServerOptions>(
    builder.Configuration.GetSection("SdkServer"));

// --- Listen on TCP + (optionally) a Unix socket for inter-service calls ---
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.ListenAnyIP(options.Port);
    if (!string.IsNullOrWhiteSpace(options.UnixSocketPath))
    {
        var sockDir = Path.GetDirectoryName(options.UnixSocketPath);
        if (!string.IsNullOrEmpty(sockDir))
            Directory.CreateDirectory(sockDir);
        if (File.Exists(options.UnixSocketPath))
            File.Delete(options.UnixSocketPath);
        kestrel.ListenUnixSocket(options.UnixSocketPath);
    }
});

// --- SQLite database ---
var sqlitePath = Path.GetFullPath(options.Database.SqlitePath);
var sqliteDir = Path.GetDirectoryName(sqlitePath);
if (!string.IsNullOrEmpty(sqliteDir))
{
    Directory.CreateDirectory(sqliteDir);
}
builder.Services.AddDbContext<SdkDbContext>(db =>
    db.UseSqlite($"Data Source={sqlitePath}"));

// --- Services ---
builder.Services.AddScoped<SessionService>();
builder.Services.AddHostedService<SessionCleanupService>();

// --- Per-IP rate limiting: 10 requests/minute on auth endpoints ---
builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limiter.AddPolicy("auth", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            ip,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            });
    });
});

var app = builder.Build();

// --- Create the database file / tables on startup ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SdkDbContext>();
    db.Database.EnsureCreated();
}

app.UseRateLimiter();

SdkEndpoints.Map(app);

app.Run();
