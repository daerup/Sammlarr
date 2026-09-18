using Domain.Feed;

namespace Domain.Tests.Feed;

public class ScheduledTests
{
	private static readonly DateTimeOffset Noon = new(2026, 9, 18, 10, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task NextRun_WhenTheDailyTimeHasPassed_ReturnsTomorrow()
	{
		var testee = new Scheduled("0 0 9 * * *");

		var next = testee.NextRun(Noon, TimeZoneInfo.Utc);

		await Assert.That(next).IsEqualTo(new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero));
	}

	[Test]
	public async Task NextRun_WhenStartingExactlyOnAnOccurrence_SkipsToTheFollowingOne()
	{
		var testee = new Scheduled("0 0 9 * * *");

		var next = testee.NextRun(new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);

		await Assert.That(next).IsEqualTo(new DateTimeOffset(2026, 9, 19, 9, 0, 0, TimeSpan.Zero));
	}

	[Test]
	public async Task NextRun_WhenTheZoneIsAheadOfUtc_ResolvesTheLocalWallClockTime()
	{
		var zone = TimeZoneInfo.CreateCustomTimeZone("Fixed+02", TimeSpan.FromHours(2), "Fixed+02", "Fixed+02");
		var testee = new Scheduled("0 0 9 * * *");

		var next = testee.NextRun(Noon, zone);

		await Assert.That(next!.Value.UtcDateTime).IsEqualTo(new DateTime(2026, 9, 19, 7, 0, 0, DateTimeKind.Utc));
	}
}
