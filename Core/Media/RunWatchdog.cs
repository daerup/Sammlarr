namespace Core.Media;

internal sealed class RunWatchdog : IAsyncDisposable
{
	private readonly TimeProvider _time;
	private readonly TimeSpan _stallAfter;
	private readonly CancellationTokenSource _starved;
	private readonly ITimer _check;
	private long _lastSettled;
	private int _open;
	private volatile bool _isStarved;

	public RunWatchdog(TimeProvider time, TimeSpan stallAfter, CancellationToken run)
	{
		_time = time;
		_stallAfter = stallAfter;
		_starved = CancellationTokenSource.CreateLinkedTokenSource(run);
		_lastSettled = time.GetTimestamp();
		_check = time.CreateTimer(_ => Check(), null, stallAfter / 4, stallAfter / 4);
	}

	public CancellationToken Token => _starved.Token;

	public bool Starved => _isStarved;

	public void Started()
	{
		if (Interlocked.Increment(ref _open) == 1)
		{
			Interlocked.Exchange(ref _lastSettled, _time.GetTimestamp());
		}
	}

	public void Settled()
	{
		Interlocked.Decrement(ref _open);
		Interlocked.Exchange(ref _lastSettled, _time.GetTimestamp());
	}

	public async ValueTask DisposeAsync()
	{
		await _check.DisposeAsync();
		_starved.Dispose();
	}

	private void Check()
	{
		if (Volatile.Read(ref _open) > 0 && _time.GetElapsedTime(Interlocked.Read(ref _lastSettled)) >= _stallAfter)
		{
			_isStarved = true;
			_starved.Cancel();
		}
	}
}
