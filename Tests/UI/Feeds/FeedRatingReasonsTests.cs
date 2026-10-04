using BlazorBlueprint.Primitives.Filtering;
using Domain.Media;
using UI.Features.Feeds.Pages.Editor;
using FilterOperator = BlazorBlueprint.Primitives.Filtering.FilterOperator;

namespace UI.Tests.Feeds;

public class FeedRatingReasonsTests
{
	private static readonly ValueSet<RatingSource> Everything =
	[
		RatingSource.Tmdb,
		RatingSource.Imdb,
		RatingSource.RottenTomatoesCritics,
		RatingSource.RottenTomatoesAudience,
	];

	private static readonly RatingSummary LastSeen = RatingSummary.From([new TmdbRating(9.0f, Some.Of(8)), new ImdbRating(6.4f, Some.Of(303))]);

	[Test]
	public async Task For_WhenTheAverageIsBelowTheRule_SaysSoWithTheAverage()
	{
		var average = new RatingStrategy(AveragedRating.Everything);
		var rating = average.Resolve(RatingSummary.From([new TmdbRating(6.4f, Some.Of(31)), new ImdbRating(6.4f, Some.Of(736))]), Everything);

		await Assert.That(FeedRatingReasons.For(Rule("Rating", 7.0), rating, average)).IsEquivalentTo(["Rating 6.4 (the average) is below 7"]);
	}

	[Test]
	public async Task For_WhenTheFirstRatingInTheOrderIsBelowTheRule_NamesIt()
	{
		var ordered = new RatingStrategy(new OrderedRating([RatingSource.Imdb]));

		await Assert
			.That(FeedRatingReasons.For(Rule("Rating", 7.0), ordered.Resolve(LastSeen, Everything), ordered))
			.IsEquivalentTo(["Rating 6.4 (IMDb 6.4, first in this feed's order) is below 7"]);
	}

	[Test]
	public async Task For_WhenTooFewVotesStandBehindTheRating_CountsThem()
	{
		var average = new RatingStrategy(AveragedRating.Everything);

		await Assert.That(FeedRatingReasons.For(Rule("Votes", 500.0), average.Resolve(LastSeen, Everything), average)).IsEquivalentTo(["Votes 311 is below 500"]);
	}

	[Test]
	public async Task For_WhenOnlyRottenTomatoesRates_SaysItPublishesNoVotes()
	{
		var ordered = new RatingStrategy(new OrderedRating([RatingSource.RottenTomatoesCritics]));
		var rating = ordered.Resolve(RatingSummary.From([new RottenTomatoesCriticsRating(87)]), Everything);

		await Assert
			.That(FeedRatingReasons.For(Rule("Votes", 500.0), rating, ordered))
			.IsEquivalentTo(["no votes behind its rating (Rotten Tomatoes critics publishes none)"]);
	}

	[Test]
	public async Task For_WhenTheAverageReadsAsTheBound_GivesNoReason()
	{
		var average = new RatingStrategy(AveragedRating.Everything);
		var rating = average.Resolve(RatingSummary.From([new TmdbRating(6.5f, Some.Of(31)), new ImdbRating(7.4f, Some.Of(736))]), Everything);

		await Assert.That(FeedRatingReasons.For(Rule("Rating", 7.0), rating, average)).IsEmpty();
	}

	[Test]
	public async Task For_WhenNothingRatesTheTitle_SaysSoForAVotesRule()
	{
		var average = new RatingStrategy(AveragedRating.Everything);

		await Assert.That(FeedRatingReasons.For(Rule("Votes", 500.0), new None(), average)).IsEquivalentTo(["no rating yet; no provider rates it"]);
	}

	[Test]
	public async Task For_WhenTheRatingCameWithoutAVoteCount_SaysNoneCameWithIt()
	{
		var average = new RatingStrategy(AveragedRating.Everything);
		var rating = average.Resolve(RatingSummary.From([new TmdbRating(10.0f, new None())]), Everything);

		await Assert.That(FeedRatingReasons.For(Rule("Votes", 500.0), rating, average)).IsEquivalentTo(["no votes behind its rating (no vote count came with it)"]);
	}

	[Test]
	public async Task For_WhenOutsideABetweenRule_NamesBothBounds()
	{
		var average = new RatingStrategy(AveragedRating.Everything);
		var between = Rule("Rating", 8.0, FilterOperator.Between, 9.0);

		await Assert
			.That(FeedRatingReasons.For(between, average.Resolve(LastSeen, Everything), average))
			.IsEquivalentTo(["Rating 7.7 (the average) is outside 8 to 9"]);
	}

	[Test]
	public async Task For_WhenTheTitlePassesTheRule_GivesNoReason()
	{
		var average = new RatingStrategy(AveragedRating.Everything);

		await Assert.That(FeedRatingReasons.For(Rule("Rating", 7.0), average.Resolve(LastSeen, Everything), average)).IsEmpty();
	}

	private static FilterDefinition Rule(string field, double value, FilterOperator comparison = FilterOperator.GreaterOrEqual, double? end = null)
	{
		return new FilterDefinition
		{
			Conditions =
			[
				new FilterCondition
				{
					Field = field,
					Operator = comparison,
					Value = value,
					ValueEnd = end,
				},
			],
		};
	}
}
