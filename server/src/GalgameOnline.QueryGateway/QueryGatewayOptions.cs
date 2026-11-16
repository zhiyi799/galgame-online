namespace GalgameOnline.QueryGateway;

/// <summary>Options bound from the "QueryGateway" configuration section.</summary>
public sealed class QueryGatewayOptions
{
    /// <summary>TCP port the gateway listens on. Default 5200.</summary>
    public int Port { get; set; } = 5200;

    /// <summary>Seconds without a heartbeat after which an instance is stale. Default 30.</summary>
    public int StaleTimeoutSeconds { get; set; } = 30;

    /// <summary>Seconds between stale-instance cleanup sweeps. Default 10.</summary>
    public int CleanupIntervalSeconds { get; set; } = 10;
}
