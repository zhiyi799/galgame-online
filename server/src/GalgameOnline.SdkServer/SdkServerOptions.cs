namespace GalgameOnline.SdkServer;

/// <summary>Options bound from the "SdkServer" section of appsettings.json.</summary>
public sealed class SdkServerOptions
{
    public int Port { get; set; } = 5000;

    /// <summary>Base URL of the GameServer, handed to clients after login.</summary>
    public string GameServerUrl { get; set; } = "http://localhost:5100";

    /// <summary>Session token lifetime in seconds (default 1 hour).</summary>
    public int TokenTtlSeconds { get; set; } = 3600;

    /// <summary>Unix socket path for inter-service calls (empty = disabled). Lets the
    /// GameServer reach the SdkServer in sandboxes where loopback TCP is blocked.</summary>
    public string UnixSocketPath { get; set; } = "/tmp/sdk.sock";

    public DatabaseOptions Database { get; set; } = new();

    public sealed class DatabaseOptions
    {
        public string SqlitePath { get; set; } = "data/sdk.db";
    }
}
