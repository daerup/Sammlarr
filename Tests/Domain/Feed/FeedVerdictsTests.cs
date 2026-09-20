using Domain.Feed.Run;
using Domain.Processing;

namespace Domain.Tests.Feed;

public class FeedVerdictsTests
{
	[Test]
	[Arguments(QueryOutcome.Qualified, false, false, false, true)]
	[Arguments(QueryOutcome.Qualified, true, false, false, false)]
	[Arguments(QueryOutcome.Qualified, false, true, false, false)]
	[Arguments(QueryOutcome.Culled, false, false, false, false)]
	[Arguments(QueryOutcome.Truncated, false, false, false, false)]
	public async Task WillImport_HoldsOnlyForAQualifiedRowTheUserHasNotEnrolled(
		QueryOutcome outcome,
		bool inWatchlist,
		bool inBlacklist,
		bool owned,
		bool expected
	) => await Assert.That(Row(outcome, inWatchlist, inBlacklist, owned).WillImport).IsEqualTo(expected);

	[Test]
	[Arguments(QueryOutcome.Qualified, false, true, true)]
	[Arguments(QueryOutcome.Qualified, true, true, true)]
	[Arguments(QueryOutcome.Qualified, false, false, false)]
	[Arguments(QueryOutcome.Culled, false, true, false)]
	public async Task Kept_HoldsForAnyQualifiedOwnedRow(QueryOutcome outcome, bool inBlacklist, bool owned, bool expected) =>
		await Assert.That(Row(outcome, inWatchlist: false, inBlacklist, owned).Kept).IsEqualTo(expected);

	[Test]
	[Arguments(QueryOutcome.Qualified, true, false, false, true)]
	[Arguments(QueryOutcome.Qualified, false, true, false, true)]
	[Arguments(QueryOutcome.Qualified, true, false, true, false)]
	[Arguments(QueryOutcome.Qualified, false, false, false, false)]
	[Arguments(QueryOutcome.Culled, true, false, false, false)]
	public async Task Bypassed_HoldsForAQualifiedEnrolledRowTheFeedDoesNotOwn(
		QueryOutcome outcome,
		bool inWatchlist,
		bool inBlacklist,
		bool owned,
		bool expected
	) => await Assert.That(Row(outcome, inWatchlist, inBlacklist, owned).Bypassed).IsEqualTo(expected);

	[Test]
	[Arguments(QueryOutcome.Culled, false, true, true)]
	[Arguments(QueryOutcome.Truncated, false, true, true)]
	[Arguments(QueryOutcome.Culled, true, true, true)]
	[Arguments(QueryOutcome.Qualified, false, true, false)]
	[Arguments(QueryOutcome.Culled, false, false, false)]
	public async Task AtRisk_HoldsForAnOwnedRowThatNoLongerQualifies(QueryOutcome outcome, bool inBlacklist, bool owned, bool expected) =>
		await Assert.That(Row(outcome, inWatchlist: false, inBlacklist, owned).AtRisk).IsEqualTo(expected);

	private static QueryVerdict<string> Row(QueryOutcome outcome, bool inWatchlist, bool inBlacklist, bool owned) =>
		new(
			"item",
			outcome,
			new Enrollment
			{
				InWatchlist = inWatchlist,
				InBlacklist = inBlacklist,
				Owned = owned,
			},
			ConsumesSlot: true
		);
}
