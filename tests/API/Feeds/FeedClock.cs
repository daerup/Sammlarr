using Microsoft.Extensions.Time.Testing;

namespace API.Tests.Feeds;

internal static class FeedClock
{
	internal static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

	internal static TimeProvider Today() => new FakeTimeProvider(Now);
}
