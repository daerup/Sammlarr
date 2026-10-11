using Domain.Usage;
using TUnit.Assertions.Enums;

namespace Domain.Tests.Usage;

public class QuotaDaysTests
{
	private static readonly DateTimeOffset NextReset = new(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task Of_AnHourBeforeTheReset_CountsToTheDayItEnds()
	{
		var testee = QuotaDays.Of([(NextReset.AddDays(-1).AddHours(-1), UsageCause.FeedRun, 40), (NextReset.AddDays(-1), UsageCause.FeedRun, 7)], NextReset, 7);

		await Assert.That(testee[^2].Spent[UsageCause.FeedRun]).IsEqualTo(40);
		await Assert.That(testee[^1].Spent[UsageCause.FeedRun]).IsEqualTo(7);
	}

	[Test]
	public async Task Of_SevenDays_StartsEachDayAtTheResetAndEndsWithToday()
	{
		var testee = QuotaDays.Of([], NextReset, 7);

		await Assert
			.That(testee.Select(day => day.Start))
			.IsEquivalentTo([.. Enumerable.Range(1, 7).Reverse().Select(days => NextReset.AddDays(-days))], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Of_ThirtyDays_LeavesOutRowsOlderThanItsFirstDay()
	{
		var testee = QuotaDays.Of([(NextReset.AddDays(-31), UsageCause.Search, 5), (NextReset.AddDays(-30), UsageCause.Search, 3)], NextReset, 30);

		await Assert.That(testee).Count().IsEqualTo(30);
		await Assert.That(testee.Sum(day => day.Total)).IsEqualTo(3);
	}

	[Test]
	public async Task Of_ADayOfSeveralCauses_AddsEachCauseUp()
	{
		var hour = NextReset.AddHours(-5);

		var testee = QuotaDays.Of([(hour, UsageCause.FeedRun, 10), (hour.AddHours(1), UsageCause.FeedRun, 5), (hour, UsageCause.DetailsFill, 2)], NextReset, 7);

		await Assert.That(testee[^1].Spent).IsEquivalentTo(new Dictionary<UsageCause, long> { [UsageCause.FeedRun] = 15, [UsageCause.DetailsFill] = 2 });
		await Assert.That(testee[^1].Total).IsEqualTo(17);
	}
}
