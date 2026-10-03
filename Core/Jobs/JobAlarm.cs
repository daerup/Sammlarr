using Contracts;
using Contracts.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Jobs;

internal sealed class JobAlarm(IServiceScopeFactory scopes, JobSignal signal, IDateTimeProvider clock, ILogger<JobAlarm> logger) : IDisposable
{
	private static readonly TimeSpan LongestWait = TimeSpan.FromMinutes(15);

	private readonly Lock _gate = new();

	private Timer? _timer;

	internal async Task ArmAsync(CancellationToken cancellationToken)
	{
		await using var scope = scopes.CreateAsyncScope();
		var due = await scope.ServiceProvider.GetRequiredService<IJobRepository>().NextWaitingAsync(cancellationToken);
		Set(due.Map(Until).GetValueOr(Timeout.InfiniteTimeSpan));
	}

	private void Set(TimeSpan delay)
	{
		lock (_gate)
		{
			_timer?.Dispose();
			_timer = delay == Timeout.InfiniteTimeSpan ? null : new Timer(_ => Release(), null, delay, Timeout.InfiniteTimeSpan);
		}
	}

	private TimeSpan Until(DateTimeOffset at)
	{
		var wait = at - clock.Now;
		return wait > LongestWait ? LongestWait
			: wait > TimeSpan.Zero ? wait
			: TimeSpan.Zero;
	}

	private async void Release()
	{
		try
		{
			await using var scope = scopes.CreateAsyncScope();
			var released = await scope.ServiceProvider.GetRequiredService<IJobRepository>().ReleaseDueAsync(clock.Now);
			if (released > 0)
			{
				signal.Raise();
			}

			await ArmAsync(CancellationToken.None);
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "A deferred job could not be released and will wait for the next enqueue");
		}
	}

	public void Dispose()
	{
		lock (_gate)
		{
			_timer?.Dispose();
			_timer = null;
		}
	}
}
