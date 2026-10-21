using Domain.Processing;
using static TestInfrastructure.QueryFilterFactory;

namespace Domain.Tests.Processing;

public class FilterChecksTests
{
	private static readonly FilterPredicate Votes = Number("Votes", FilterPredicateOperator.GreaterThan, 5);
	private static readonly FilterPredicate Episodes = Number("Episodes", FilterPredicateOperator.LessThan, 200);
	private static readonly FilterPredicate IsMovie = String("MediaType", FilterPredicateOperator.Equals, "Movie");
	private static readonly FilterPredicate Open = Number("Votes", FilterPredicateOperator.Between, 10);

	[Test]
	public async Task Fails_ForAnAndGroup_FailsOnAnyFailedRule()
	{
		var group = Group(Votes, Episodes);

		await Assert.That(Checked(failed: [Episodes]).Fails(group)).IsTrue();
		await Assert.That(Checked(failed: []).Fails(group)).IsFalse();
	}

	[Test]
	public async Task Fails_ForAnOrGroup_FailsOnlyWhenEveryOptionFails()
	{
		var filter = Filter(Group(FilterOperator.Or, [], [Group(Episodes), Group(IsMovie)]));

		await Assert.That(Checked(failed: [Episodes]).Fails(filter)).IsFalse();
		await Assert.That(Checked(failed: [Episodes, IsMovie]).Fails(filter)).IsTrue();
	}

	[Test]
	public async Task Fails_ForTheSameRuleInTwoGroups_AnswersBothAlike()
	{
		var group = Group(FilterOperator.Or, [], [Group(IsMovie, Votes), Group(IsMovie, Episodes)]);

		await Assert.That(group.Groups.Select(Checked(failed: [IsMovie]).Fails)).IsEquivalentTo([true, true]);
	}

	[Test]
	public async Task Fails_ForARuleThatWasNeverChecked_LeavesItOut()
	{
		var group = Group(FilterOperator.Or, Open, IsMovie);

		await Assert.That(Checked(failed: [IsMovie]).Fails(group)).IsTrue();
	}

	[Test]
	public async Task Fails_ForAGroupWithNothingChecked_LeavesItOut()
	{
		var group = Group(FilterOperator.Or, [IsMovie], [Group(Open)]);

		await Assert.That(Checked(failed: [IsMovie]).Fails(group)).IsTrue();
	}

	private static FilterChecks Checked(ValueSet<FilterPredicate> failed) => new([Votes, Episodes, IsMovie], failed);
}
