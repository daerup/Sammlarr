using System.Collections.Concurrent;
using Domain.Feed;

namespace Core.Feed;

internal sealed class FeedRunGate
{
	private readonly ConcurrentDictionary<FeedId, SemaphoreSlim> _gates = new();

	internal async Task<Optional<IDisposable>> EnterAsync(FeedId feedId, CancellationToken cancellationToken = default)
	{
		var gate = _gates.GetOrAdd(feedId, _ => new SemaphoreSlim(1, 1));
		return Optional.When<IDisposable>(await gate.WaitAsync(0, cancellationToken), () => new Lease(gate));
	}

	private sealed class Lease(SemaphoreSlim gate) : IDisposable
	{
		public void Dispose() => gate.Release();
	}
}
