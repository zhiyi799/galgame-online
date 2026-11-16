using GalgameOnline.QueryGateway;
using GalgameOnline.Shared;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<QueryGatewayOptions>()
    .BindConfiguration("QueryGateway")
    .Validate(o => o.Port is > 0 and <= 65535, "QueryGateway.Port must be a valid TCP port")
    .ValidateOnStart();

builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<QueryGatewayOptions>>().Value);
builder.Services.AddSingleton<InstanceRegistry>();
builder.Services.AddHostedService<GatewayCleanupService>();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var gateway = app.MapGroup("/api/gateway");

// Register (or re-register) a GameServer instance.
gateway.MapPost("/instances/register", (RegisterRequest request, InstanceRegistry registry) =>
{
    if (string.IsNullOrWhiteSpace(request.InstanceId) || string.IsNullOrWhiteSpace(request.Url))
        return Results.BadRequest(new RegisterResponse(false, "instanceId and url are required"));
    if (request.MaxPlayers <= 0)
        return Results.BadRequest(new RegisterResponse(false, "maxPlayers must be greater than 0"));

    registry.Register(request);
    return Results.Ok(new RegisterResponse(true, null));
});

// Heartbeat from a GameServer instance: refreshes its player count and last-seen time.
gateway.MapPost("/instances/heartbeat", (HeartbeatRequest request, InstanceRegistry registry) =>
{
    if (!registry.Heartbeat(request))
        return Results.NotFound(new { ok = false, error = "unknown instance" });
    return Results.Ok(new { ok = true });
});

// Allocate a GameServer for a client: lowest load ratio, round-robin among ties.
gateway.MapPost("/allocate", (InstanceRegistry registry) =>
    Results.Ok(registry.Allocate()));

// List all registered instances.
gateway.MapGet("/instances", (InstanceRegistry registry) =>
    Results.Ok(registry.ListAll()));

// Gateway health + instance count.
gateway.MapGet("/status", (InstanceRegistry registry) =>
    Results.Ok(new { ok = true, instances = registry.Count }));

var options = app.Services.GetRequiredService<QueryGatewayOptions>();
app.Urls.Clear();
app.Urls.Add($"http://0.0.0.0:{options.Port}");

app.Logger.LogInformation("QueryGateway listening on 0.0.0.0:{Port}", options.Port);
app.Run();
