using Domain.Feed;
using TestInfrastructure;

namespace Domain.Tests.Feed;

public class FeedRunTriggerTests
{
	private static readonly DateTimeOffset Noon = new(2026, 9, 18, 10, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task NextRun_WhenScheduled_IsTheScheduleNextOccurrence()
	{
		FeedRunTrigger testee = new Scheduled("0 0 9 * * *");

		await Assert.That(testee.NextRun(Noon, TimeZoneInfo.Utc)).IsSome().And.IsEqualTo(new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero));
	}

	[Test]
	public async Task NextRun_WhenManual_IsNone()
	{
		FeedRunTrigger testee = new Manual();

		await Assert.That(testee.NextRun(Noon, TimeZoneInfo.Utc)).IsNone();
	}
}
