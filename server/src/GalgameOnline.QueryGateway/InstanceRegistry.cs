using System.Collections.Concurrent;
using GalgameOnline.Shared;

namespace GalgameOnline.QueryGateway;

/// <summary>
/// In-memory registry of live GameServer instances.
/// Single-gateway deployment: no database, state lives in this process.
/// </summary>
public sealed class InstanceRegistry
{
    private readonly ConcurrentDictionary<string, InstanceEntry> _instances = new(StringComparer.Ordinal);
    private long _allocateCounter;

    /// <summary>Adds a new instance or updates an existing one. Resets players to 0 and stamps the heartbeat time.</summary>
    public InstanceEntry Register(RegisterRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        return _instances.AddOrUpdate(
            request.InstanceId,
            _ => new InstanceEntry
            {
                InstanceId = request.InstanceId,
                Url = request.Url,
                Port = request.Port,
                MaxPlayers = request.MaxPlayers,
                Players = 0,
                LastHeartbeat = now,
            },
            (_, existing) =>
            {
                existing.Url = request.Url;
                existing.Port = request.Port;
                existing.MaxPlayers = request.MaxPlayers;
                existing.Players = 0;
                existing.LastHeartbeat = now;
                return existing;
            });
    }

    /// <summary>Refreshes an instance's player count and heartbeat time. Returns false if the instance is unknown.</summary>
    public bool Heartbeat(HeartbeatRequest request)
    {
        if (!_instances.TryGetValue(request.InstanceId, out var entry))
            return false;

        entry.Players = Math.Max(0, request.Players);
        entry.LastHeartbeat = DateTimeOffset.UtcNow;
        return true;
    }

    /// <summary>
    /// Picks the registered instance with the lowest Players/MaxPlayers ratio.
    /// Among ties at the lowest ratio, picks round-robin.
    /// Returns Ok=false when no instance is registered.
    /// </summary>
    public AllocateResponse Allocate()
    {
        var candidates = _instances.Values
            .Where(e => e.MaxPlayers > 0)
            .Select(e => (Entry: e, Ratio: (double)e.Players / e.MaxPlayers))
            .ToList();

        if (candidates.Count == 0)
            return new AllocateResponse(false, "no game server available", null);

        var minRatio = candidates.Min(c => c.Ratio);
        var best = candidates
            .Where(c => c.Ratio == minRatio)
            .Select(c => c.Entry)
            .ToList();

        var index = (int)((Interlocked.Increment(ref _allocateCounter) - 1) % best.Count);
        return new AllocateResponse(true, null, best[index].Url);
    }

    /// <summary>All registered instances, ordered by id.</summary>
    public List<InstanceSummary> ListAll() =>
        _instances.Values
            .Select(e => new InstanceSummary(e.InstanceId, e.Url, e.Players, e.MaxPlayers, e.LastHeartbeat))
            .OrderBy(s => s.InstanceId, StringComparer.Ordinal)
            .ToList();

    public int Count => _instances.Count;

    /// <summary>Removes instances whose last heartbeat is older than <paramref name="staleAfter"/>. Returns how many were removed.</summary>
    public int RemoveStale(TimeSpan staleAfter)
    {
        var cutoff = DateTimeOffset.UtcNow - staleAfter;
        var staleIds = _instances
            .Where(kv => kv.Value.LastHeartbeat < cutoff)
            .Select(kv => kv.Key)
            .ToList();

        var removed = 0;
        foreach (var id in staleIds)
        {
            if (_instances.TryRemove(id, out _))
                removed++;
        }
        return removed;
    }
}
