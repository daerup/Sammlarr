using Domain.Feed.Run;
using Domain.Processing;
using TestInfrastructure;

namespace Domain.Tests.Feed;

public class FeedVerdictsTests
{
	[Test]
	[Arguments(OutcomeCase.Qualified, false, false, false, true)]
	[Arguments(OutcomeCase.Qualified, true, false, false, false)]
	[Arguments(OutcomeCase.Qualified, false, true, false, false)]
	[Arguments(OutcomeCase.Culled, false, false, false, false)]
	[Arguments(OutcomeCase.Truncated, false, false, false, false)]
	public async Task WillImport_HoldsOnlyForAQualifiedRowTheUserHasNotEnrolled(
		OutcomeCase outcome,
		bool inWantlist,
		bool inBlocklist,
		bool owned,
		bool expected
	) => await Assert.That(Row(outcome, inWantlist, inBlocklist, owned).WillImport).IsEqualTo(expected);

	[Test]
	[Arguments(OutcomeCase.Qualified, false, true, true)]
	[Arguments(OutcomeCase.Qualified, true, true, true)]
	[Arguments(OutcomeCase.Qualified, false, false, false)]
	[Arguments(OutcomeCase.Culled, false, true, false)]
	public async Task Kept_HoldsForAnyQualifiedOwnedRow(OutcomeCase outcome, bool inBlocklist, bool owned, bool expected) =>
		await Assert.That(Row(outcome, inWantlist: false, inBlocklist, owned).Kept).IsEqualTo(expected);

	[Test]
	[Arguments(OutcomeCase.Qualified, true, false, false, true)]
	[Arguments(OutcomeCase.Qualified, false, true, false, true)]
	[Arguments(OutcomeCase.Qualified, true, false, true, false)]
	[Arguments(OutcomeCase.Qualified, false, false, false, false)]
	[Arguments(OutcomeCase.Culled, true, false, false, false)]
	public async Task Bypassed_HoldsForAQualifiedEnrolledRowTheFeedDoesNotOwn(
		OutcomeCase outcome,
		bool inWantlist,
		bool inBlocklist,
		bool owned,
		bool expected
	) => await Assert.That(Row(outcome, inWantlist, inBlocklist, owned).Bypassed).IsEqualTo(expected);

	[Test]
	[Arguments(OutcomeCase.Culled, false, true, true)]
	[Arguments(OutcomeCase.Truncated, false, true, true)]
	[Arguments(OutcomeCase.Culled, true, true, true)]
	[Arguments(OutcomeCase.Qualified, false, true, false)]
	[Arguments(OutcomeCase.Culled, false, false, false)]
	public async Task AtRisk_HoldsForAnOwnedRowThatNoLongerQualifies(OutcomeCase outcome, bool inBlocklist, bool owned, bool expected) =>
		await Assert.That(Row(outcome, inWantlist: false, inBlocklist, owned).AtRisk).IsEqualTo(expected);

	private static QueryVerdict<string> Row(OutcomeCase outcome, bool inWantlist, bool inBlocklist, bool owned) =>
		new(
			"item",
			new Enrollment
			{
				InWantlist = inWantlist,
				InBlocklist = inBlocklist,
				Owned = owned,
			},
			outcome.Outcome
		);
}
