using System.Collections.Concurrent;
using Domain.Identity;

namespace Core.Wantlist.Sync;

internal sealed class WantlistSyncGate
{
	private readonly ConcurrentDictionary<UserId, SemaphoreSlim> _gates = new();

	internal async Task<Optional<IDisposable>> EnterAsync(UserId userId, CancellationToken cancellationToken = default)
	{
		var gate = _gates.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
		return Optional.When<IDisposable>(await gate.WaitAsync(0, cancellationToken), () => new Lease(gate));
	}

	private sealed class Lease(SemaphoreSlim gate) : IDisposable
	{
		public void Dispose() => gate.Release();
	}
}
