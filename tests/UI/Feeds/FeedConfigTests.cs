using BlazorBlueprint.Primitives.Filtering;
using Contracts.Processing;
using Domain.Processing;
using Domain.Providers;
using TestInfrastructure;
using UI.Features.Feeds.Pages.Editor;

namespace UI.Tests.Feeds;

public class FeedConfigTests
{
	private static readonly FeedConfig Complete = new()
	{
		Name = "Documentaries",
		Provider = FeedProvider.Mdblist,
		Identifier = "hdlists/documentaries",
	};

	[Test]
	public async Task Problem_ForACompleteFeed_IsNone()
	{
		await Assert.That(Complete.Problem).IsNone();
	}

	[Test]
	public async Task Problem_ForEachMissingPiece_NamesItInFormOrder()
	{
		await Assert.That(await ProblemOf(Complete with { Name = " ", Identifier = "" })).IsEqualTo("Name is required");
		await Assert.That(await ProblemOf(Complete with { Provider = null })).IsEqualTo("Pick a provider");
		await Assert.That(await ProblemOf(Complete with { IsProviderAvailable = false })).IsEqualTo("The provider's API key is missing");
		await Assert.That(await ProblemOf(Complete with { Identifier = " " })).IsEqualTo("List identifier is required");
		await Assert.That(await ProblemOf(Complete with { Limit = new QueryLimit { Count = 0 } })).IsEqualTo("Limit must be a positive whole number");
		await Assert.That(await ProblemOf(Complete with { IsScheduleValid = false })).IsEqualTo("The schedule is incomplete");
	}

	[Test]
	public async Task Problem_ForAnUnfinishedCondition_NamesItsField()
	{
		var condition = new FilterCondition { Field = nameof(MediaQueryRow.MediaType), Operator = BlazorBlueprint.Primitives.Filtering.FilterOperator.In };
		var config = Complete with { Filter = new FilterDefinition { Conditions = [condition] } };

		var problem = await Assert.That(config.Problem).IsSome();

		await Assert.That(problem).IsEqualTo("Media Type condition unfinished");
	}

	[Test]
	public async Task SameSource_IgnoresSurroundingSpacesAndEverythingButTheList()
	{
		var other = Complete with
		{
			Name = "Other",
			Identifier = " hdlists/documentaries ",
			Limit = new QueryLimit { Count = 5 },
		};

		await Assert.That(Complete.SameSource(other)).IsTrue();
		await Assert.That(Complete.SameSource(Complete with { Identifier = "hdlists/comedy" })).IsFalse();
		await Assert.That(Complete.SameSource(Complete with { Provider = FeedProvider.Tmdb })).IsFalse();
	}

	private static async Task<string?> ProblemOf(FeedConfig config) => await Assert.That(config.Problem).IsSome();
}
