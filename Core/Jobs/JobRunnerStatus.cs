using Contracts.Jobs;

namespace Core.Jobs;

internal sealed class JobRunnerStatus : IJobRunnerStatus
{
	private const int NotStarted = 0;
	private const int Started = 1;
	private const int Finished = 2;

	private int _state;
	private int _consecutiveFailures;
	private long _lastFailureAt;

	public bool IsRunning => Volatile.Read(ref _state) == Started;

	public bool HasStopped => Volatile.Read(ref _state) == Finished;

	public int ConsecutiveFailures => Volatile.Read(ref _consecutiveFailures);

	public DateTimeOffset? LastFailureAt => Volatile.Read(ref _lastFailureAt) is var ticks && ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);

	internal IDisposable Running()
	{
		Volatile.Write(ref _state, Started);
		return new Session(this);
	}

	internal void Succeeded() => Volatile.Write(ref _consecutiveFailures, 0);

	internal void Failed(DateTimeOffset at)
	{
		Volatile.Write(ref _lastFailureAt, at.UtcDateTime.Ticks);
		Interlocked.Increment(ref _consecutiveFailures);
	}

	private void Stop() => Volatile.Write(ref _state, Finished);

	private sealed class Session(JobRunnerStatus status) : IDisposable
	{
		public void Dispose() => status.Stop();
	}
}
