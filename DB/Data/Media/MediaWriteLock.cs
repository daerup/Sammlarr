namespace DB.Data.Media;

internal sealed class MediaWriteLock : IDisposable
{
	private readonly SemaphoreSlim _gate = new(1, 1);

	public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken)
	{
		await _gate.WaitAsync(cancellationToken);
		return new Release(_gate);
	}

	public void Dispose() => _gate.Dispose();

	private sealed class Release(SemaphoreSlim gate) : IDisposable
	{
		public void Dispose() => gate.Release();
	}
}
