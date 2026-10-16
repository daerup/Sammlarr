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
		bool inWantlist,
		bool inBlocklist,
		bool owned,
		bool expected
	) => await Assert.That(Row(outcome, inWantlist, inBlocklist, owned).WillImport).IsEqualTo(expected);

	[Test]
	[Arguments(QueryOutcome.Qualified, false, true, true)]
	[Arguments(QueryOutcome.Qualified, true, true, true)]
	[Arguments(QueryOutcome.Qualified, false, false, false)]
	[Arguments(QueryOutcome.Culled, false, true, false)]
	public async Task Kept_HoldsForAnyQualifiedOwnedRow(QueryOutcome outcome, bool inBlocklist, bool owned, bool expected) =>
		await Assert.That(Row(outcome, inWantlist: false, inBlocklist, owned).Kept).IsEqualTo(expected);

	[Test]
	[Arguments(QueryOutcome.Qualified, true, false, false, true)]
	[Arguments(QueryOutcome.Qualified, false, true, false, true)]
	[Arguments(QueryOutcome.Qualified, true, false, true, false)]
	[Arguments(QueryOutcome.Qualified, false, false, false, false)]
	[Arguments(QueryOutcome.Culled, true, false, false, false)]
	public async Task Bypassed_HoldsForAQualifiedEnrolledRowTheFeedDoesNotOwn(
		QueryOutcome outcome,
		bool inWantlist,
		bool inBlocklist,
		bool owned,
		bool expected
	) => await Assert.That(Row(outcome, inWantlist, inBlocklist, owned).Bypassed).IsEqualTo(expected);

	[Test]
	[Arguments(QueryOutcome.Culled, false, true, true)]
	[Arguments(QueryOutcome.Truncated, false, true, true)]
	[Arguments(QueryOutcome.Culled, true, true, true)]
	[Arguments(QueryOutcome.Qualified, false, true, false)]
	[Arguments(QueryOutcome.Culled, false, false, false)]
	public async Task AtRisk_HoldsForAnOwnedRowThatNoLongerQualifies(QueryOutcome outcome, bool inBlocklist, bool owned, bool expected) =>
		await Assert.That(Row(outcome, inWantlist: false, inBlocklist, owned).AtRisk).IsEqualTo(expected);

	private static QueryVerdict<string> Row(QueryOutcome outcome, bool inWantlist, bool inBlocklist, bool owned) =>
		new(
			"item",
			outcome,
			new Enrollment
			{
				InWantlist = inWantlist,
				InBlocklist = inBlocklist,
				Owned = owned,
			},
			ConsumesSlot: true
		);
}
