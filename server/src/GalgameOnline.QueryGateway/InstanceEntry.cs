namespace GalgameOnline.QueryGateway;

/// <summary>A registered GameServer instance and its live load state.</summary>
public sealed class InstanceEntry
{
    public required string InstanceId { get; init; }
    public required string Url { get; set; }
    public required int Port { get; set; }
    public required int MaxPlayers { get; set; }
    public int Players { get; set; }
    public DateTimeOffset LastHeartbeat { get; set; }
}
