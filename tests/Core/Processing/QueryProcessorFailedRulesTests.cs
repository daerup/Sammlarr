using Contracts.Processing;
using Core.Processing;
using Domain.Media;
using Domain.Processing;
using TestInfrastructure;
using static TestInfrastructure.QueryFilterFactory;

namespace Core.Tests.Processing;

public class QueryProcessorFailedRulesTests
{
	private static readonly FilterPredicate Votes = Number(nameof(MediaQueryRow.Votes), FilterPredicateOperator.GreaterThan, 5);
	private static readonly FilterPredicate FewEpisodes = Number(nameof(MediaQueryRow.Episodes), FilterPredicateOperator.LessThan, 200);
	private static readonly FilterPredicate IsShow = String(nameof(MediaQueryRow.MediaType), FilterPredicateOperator.Equals, "Show");
	private static readonly FilterPredicate IsMovie = String(nameof(MediaQueryRow.MediaType), FilterPredicateOperator.Equals, "Movie");

	private static readonly QueryFilter VotedShortShowsOrMovies = Filter(
		Group(FilterOperator.And, [Votes], [Group(FilterOperator.Or, [], [Group(FilterOperator.And, FewEpisodes, IsShow), Group(FilterOperator.And, IsMovie)])])
	);

	private readonly QueryProcessor<MediaQueryRow> _testee = new();

	[Test]
	public async Task Execute_WhenATopLevelRuleCulls_FailedHoldsEveryFailedRule()
	{
		var checks = await ChecksAsync(VotedShortShowsOrMovies, Show(episodes: 11, votes: 2));

		await Assert.That(checks.Failed).IsEquivalentTo([Votes, IsMovie]);
	}

	[Test]
	public async Task Execute_WhenEveryOrOptionFails_FailedHoldsEachOptionsRule()
	{
		var checks = await ChecksAsync(VotedShortShowsOrMovies, Show(episodes: 1219, votes: 5604));

		await Assert.That(checks.Failed).IsEquivalentTo([FewEpisodes, IsMovie]);
	}

	[Test]
	public async Task Execute_ForARelativeDateRule_FailedHoldsTheRuleAsWritten()
	{
		var recent = Date(nameof(MediaQueryRow.ReleaseDate), FilterPredicateOperator.GreaterThan, new RelativeDate(30, DateUnit.Days, Ahead: false));

		var checks = await ChecksAsync(Filter(Group(recent)), Show(episodes: 11, votes: 2));

		await Assert.That(checks.Failed).IsEquivalentTo([recent]);
	}

	[Test]
	public async Task Execute_ForAnIncompleteBetween_NeverChecksIt()
	{
		var open = Number(nameof(MediaQueryRow.Votes), FilterPredicateOperator.Between, 10);

		var checks = await ChecksAsync(Filter(Group(open, IsMovie)), Show(episodes: 11, votes: 2));

		await Assert.That(checks.Failed).IsEquivalentTo([IsMovie]);
		await Assert.That(checks.Counts(open)).IsFalse();
	}

	[Test]
	public async Task Execute_ForAnEpisodesRuleOnAMovie_FailsIt()
	{
		var checks = await ChecksAsync(Filter(Group(FewEpisodes)), Movie(votes: 300));

		await Assert.That(checks.Failed).IsEquivalentTo([FewEpisodes]);
	}

	[Test]
	public async Task Execute_ForTheSameRuleInTwoGroups_FailsItOnce()
	{
		var twice = Filter(Group(FilterOperator.Or, [], [Group(FilterOperator.And, IsMovie, Votes), Group(FilterOperator.And, IsMovie, FewEpisodes)]));

		var checks = await ChecksAsync(twice, Show(episodes: 11, votes: 2));

		await Assert.That(checks.Failed).IsEquivalentTo([IsMovie, Votes]);
	}

	private async Task<FilterChecks> ChecksAsync(QueryFilter filter, MediaQueryRow row)
	{
		var options = new QueryOptions { Filter = Some.Of(filter), Today = new DateOnly(2026, 10, 21) };
		var verdict = (await _testee.ExecuteAsync([row], options, NoEnrollment).ToListAsync()).Single();
		var culled = await Assert.That(verdict.Outcome).IsOfUnionType<QueryOutcome, Culled>();
		return culled.Checks;
	}

	private static QueryPredicates<MediaQueryRow> NoEnrollment =>
		new(IsWantlisted: _ => false, IsBlocklisted: _ => false, IsOwned: _ => false, Identity: row => row.Media.Identity.Id.Value);

	private static MediaQueryRow Show(int episodes, int votes)
	{
		return Row(
			$"show-{episodes}-{votes}",
			new ShowDetails
			{
				Title = "Show",
				Episodes = episodes,
				ReleaseDate = new DateOnly(1999, 10, 20),
				Ratings = MediaRatings.From([new TmdbRating(8.0f, Some.Of(votes))]),
			}
		);
	}

	private static MediaQueryRow Movie(int votes)
	{
		return Row($"movie-{votes}", new MovieDetails { Title = "Movie", Ratings = MediaRatings.From([new TmdbRating(7.0f, Some.Of(votes))]) });
	}

	private static MediaQueryRow Row(string id, MediaDetails details)
	{
		return MediaQueryRow.From(
			new MatchedMedia(new MediaIdentity(MediaId.New(), [TmdbId.From(id, details.Type)]), details),
			new RatingStrategy(AveragedRating.Everything),
			[RatingSource.Tmdb]
		);
	}
}
