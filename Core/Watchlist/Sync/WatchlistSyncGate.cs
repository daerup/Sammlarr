namespace Core.Watchlist.Sync;

internal sealed class WatchlistSyncGate
{
	private readonly SemaphoreSlim _gate = new(1, 1);

	internal async Task<Optional<IDisposable>> EnterAsync(CancellationToken cancellationToken = default) =>
		await _gate.WaitAsync(0, cancellationToken) ? Some.Of<IDisposable>(new Lease(_gate)) : new None();

	private sealed class Lease(SemaphoreSlim gate) : IDisposable
	{
		public void Dispose() => gate.Release();
	}
}
