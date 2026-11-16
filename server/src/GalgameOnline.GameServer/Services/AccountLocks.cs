// Per-account lock: serializes wallet/gacha mutations for one account so two
// simultaneous pulls cannot double-spend. Different accounts run in parallel.

using System.Collections.Concurrent;

namespace GalgameOnline.GameServer.Services;

public sealed class AccountLocks
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> AcquireAsync(Guid accountId, CancellationToken ct = default)
    {
        var sem = _locks.GetOrAdd(accountId, static _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        return new Releaser(sem);
    }

    private sealed class Releaser(SemaphoreSlim sem) : IDisposable
    {
        public void Dispose() => sem.Release();
    }
}
