using BlazorBlueprint.Components;
using Bunit;
using Contracts.Processing;
using Domain.Media;
using Domain.Processing;
using TUnit.Assertions.Enums;
using UI.Features.Feeds.Pages.Editor.Preview;
using static TestInfrastructure.QueryFilterFactory;

namespace UI.Tests.Feeds.Preview;

public class CulledDetailTests : BunitContext
{
	internal static readonly FilterPredicate Votes = Number(nameof(MediaQueryRow.Votes), FilterPredicateOperator.GreaterThan, 5);
	internal static readonly FilterPredicate FewEpisodes = Number(nameof(MediaQueryRow.Episodes), FilterPredicateOperator.LessThan, 200);
	internal static readonly FilterPredicate IsShow = String(nameof(MediaQueryRow.MediaType), FilterPredicateOperator.Equals, "Show");
	internal static readonly FilterPredicate IsMovie = String(nameof(MediaQueryRow.MediaType), FilterPredicateOperator.Equals, "Movie");

	internal static readonly QueryFilter VotedShortShowsOrMovies = Filter(
		Group(FilterOperator.And, [Votes], [Group(FilterOperator.Or, [], [Group(FilterOperator.And, FewEpisodes, IsShow), Group(FilterOperator.And, IsMovie)])])
	);

	private static readonly RatingStrategy Average = new(AveragedRating.Everything);

	[Before(Test)]
	public void Setup() => Services.AddBlazorBlueprintComponents();

	[Test]
	public async Task Render_ForOnePiece_DrawsTheAnyOfWithBothOptions()
	{
		var cut = RenderDetail(VotedShortShowsOrMovies, [Votes, FewEpisodes, IsShow, IsMovie], [FewEpisodes, IsMovie], Show(episodes: 1219, votes: 5604));

		await Assert.That(cut.FindAll(".culled-group-label").Select(label => label.TextContent)).IsEquivalentTo(["any of", "all of"], CollectionOrdering.Matching);
		await Assert
			.That(cut.FindAll(".culled-rule").Select(rule => rule.TextContent.Words()))
			.IsEquivalentTo(["Episodes less than 200 episodes 1,219 episodes, failed", "Media Type equals Movie Show, failed"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Render_ForOnePiece_FoldsMediaTypeShowIntoOneRulePassed()
	{
		var cut = RenderDetail(VotedShortShowsOrMovies, [Votes, FewEpisodes, IsShow, IsMovie], [FewEpisodes, IsMovie], Show(episodes: 1219, votes: 5604));

		await Assert.That(cut.Find(".culled-fold").TextContent).IsEqualTo("and 1 rule that passed");
		await Assert.That(cut.Markup).DoesNotContain("Votes");
	}

	[Test]
	public async Task Render_ForATopLevelRule_DrawsNoBar()
	{
		var cut = RenderDetail(VotedShortShowsOrMovies, [Votes, FewEpisodes, IsShow, IsMovie], [Votes, IsMovie], Show(episodes: 11, votes: 2));

		await Assert.That(cut.FindAll(".culled-rule").Select(rule => rule.TextContent.Words())).IsEquivalentTo(["Votes greater than 5 2 votes, failed"]);
		await Assert.That(cut.FindAll(".culled-group")).IsEmpty();
	}

	[Test]
	public async Task Render_ForAFailedAllOfWithAPassedSubgroup_LeavesTheSubgroupOut()
	{
		var manyVotes = Number(nameof(MediaQueryRow.Votes), FilterPredicateOperator.GreaterThan, 1000);
		var filter = Filter(
			Group(FilterOperator.Or, [], [Group(FilterOperator.And, [IsMovie], [Group(FilterOperator.Or, FewEpisodes, Votes)]), Group(FilterOperator.And, manyVotes)])
		);

		var cut = RenderDetail(filter, [IsMovie, FewEpisodes, manyVotes], [IsMovie, manyVotes], Show(episodes: 11, votes: 500));

		await Assert.That(cut.FindAll(".culled-group-label").Select(label => label.TextContent)).IsEquivalentTo(["any of"]);
		await Assert
			.That(cut.FindAll(".culled-rule").Select(rule => rule.TextContent.Words()))
			.IsEquivalentTo(["Media Type equals Movie Show, failed", "Votes greater than 1,000 500 votes, failed"], CollectionOrdering.Matching);
		await Assert.That(cut.Markup).DoesNotContain("Episodes");
	}

	[Test]
	public async Task Render_ForARelativeDate_ShowsTheRuleAsWritten()
	{
		var recent = Date(nameof(MediaQueryRow.ReleaseDate), FilterPredicateOperator.GreaterThan, new RelativeDate(30, DateUnit.Days, Ahead: false));

		var cut = RenderDetail(Filter(Group(recent)), [recent], [recent], Show(episodes: 11, votes: 2));

		await Assert.That(cut.Find(".culled-rule").TextContent.Words()).IsEqualTo("Release Date is after 30 days ago 20 Oct 1999, failed");
	}

	private IRenderedComponent<CulledDetail> RenderDetail(
		QueryFilter filter,
		ValueSet<FilterPredicate> checkedRules,
		ValueSet<FilterPredicate> failed,
		MediaQueryRow row
	)
	{
		return Render<CulledDetail>(parameters =>
			parameters
				.Add(detail => detail.Filter, filter)
				.Add(detail => detail.Checks, new FilterChecks(checkedRules, failed))
				.Add(detail => detail.Row, row)
				.Add(detail => detail.Strategy, Average)
		);
	}

	internal static MediaQueryRow Show(int episodes, int votes)
	{
		var details = new ShowDetails
		{
			Title = "One Piece",
			Episodes = episodes,
			ReleaseDate = new DateOnly(1999, 10, 20),
			Ratings = MediaRatings.From([new TmdbRating(8.8f, Some.Of(votes))]),
		};
		return MediaQueryRow.From(
			new MatchedMedia(new MediaIdentity(MediaId.New(), [TmdbId.From("37854", MediaType.Show)]), details),
			Average,
			[RatingSource.Tmdb]
		);
	}
}
