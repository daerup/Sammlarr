using Contracts;

namespace Core.Media;

internal static class ClockExtensions
{
	extension(IDateTimeProvider clock)
	{
		public Task WaitUntilAsync(DateTimeOffset until, CancellationToken cancellationToken)
		{
			var remaining = until - clock.Now;
			return Task.Delay(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, cancellationToken);
		}
	}
}
