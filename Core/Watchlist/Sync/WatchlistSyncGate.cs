using System.Collections.Concurrent;
using Domain.Identity;

namespace Core.Watchlist.Sync;

internal sealed class WatchlistSyncGate
{
	private readonly ConcurrentDictionary<UserId, SemaphoreSlim> _gates = new();

	internal async Task<Optional<IDisposable>> EnterAsync(UserId userId, CancellationToken cancellationToken = default)
	{
		var gate = _gates.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
		return await gate.WaitAsync(0, cancellationToken) ? Some.Of<IDisposable>(new Lease(gate)) : new None();
	}

	private sealed class Lease(SemaphoreSlim gate) : IDisposable
	{
		public void Dispose() => gate.Release();
	}
}
