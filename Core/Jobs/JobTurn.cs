namespace Core.Jobs;

internal sealed class JobTurn
{
	private readonly SemaphoreSlim _turn = new(1, 1);

	internal async Task<IDisposable> TakeAsync(CancellationToken cancellationToken)
	{
		await _turn.WaitAsync(cancellationToken);
		return new Release(_turn);
	}

	private sealed class Release(SemaphoreSlim turn) : IDisposable
	{
		public void Dispose() => turn.Release();
	}
}
