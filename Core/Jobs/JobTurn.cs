namespace Core.Jobs;

internal sealed class JobTurn : IDisposable
{
	private readonly SemaphoreSlim _turn = new(1, 1);

	internal bool Taken => _turn.CurrentCount == 0;

	internal async Task<IDisposable> TakeAsync(CancellationToken cancellationToken)
	{
		await _turn.WaitAsync(cancellationToken);
		return new Release(_turn);
	}

	public void Dispose() => _turn.Dispose();

	private sealed class Release(SemaphoreSlim turn) : IDisposable
	{
		public void Dispose() => turn.Release();
	}
}
