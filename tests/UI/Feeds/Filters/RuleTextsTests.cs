using BlazorBlueprint.Primitives.Filtering;
using Contracts.Processing;
using Domain.Media;
using Domain.Processing;
using UI.Features.Feeds.Pages.Editor.Filters;
using Duration = Domain.Processing.Duration;
using DurationUnit = Domain.Processing.DurationUnit;
using FilterOperator = BlazorBlueprint.Primitives.Filtering.FilterOperator;

namespace UI.Tests.Feeds.Filters;

public class RuleTextsTests
{
	private static readonly ValueSet<RatingSource> Everything =
	[
		RatingSource.Tmdb,
		RatingSource.Imdb,
		RatingSource.RottenTomatoesCritics,
		RatingSource.RottenTomatoesAudience,
	];

	private static readonly MediaRatings LastSeen = MediaRatings.From([new TmdbRating(9.0f, Some.Of(8)), new ImdbRating(6.4f, Some.Of(303))]);

	private static readonly RatingStrategy Average = new(AveragedRating.Everything);

	[Test]
	public async Task Value_ForAnAveragedRating_SaysItIsTheAverage()
	{
		var rated = MediaRatings.From([new TmdbRating(6.4f, Some.Of(31)), new ImdbRating(6.4f, Some.Of(736))]);

		await Assert.That(RuleTexts.Value(nameof(MediaQueryRow.Rating), Movie(rated, Average), Average)).IsEqualTo("6.4 (the average)");
	}

	[Test]
	public async Task Value_ForTheFirstRatingInTheOrder_NamesIt()
	{
		var ordered = new RatingStrategy(new OrderedRating([RatingSource.Imdb]));

		await Assert.That(RuleTexts.Value(nameof(MediaQueryRow.Rating), Movie(LastSeen, ordered), ordered)).IsEqualTo("6.4 (IMDb 6.4, first in this feed's order)");
	}

	[Test]
	public async Task Value_ForVotes_CountsThem()
	{
		await Assert.That(RuleTexts.Value(nameof(MediaQueryRow.Votes), Movie(LastSeen, Average), Average)).IsEqualTo("311 votes");
	}

	[Test]
	public async Task Value_WhenOnlyRottenTomatoesRates_SaysItPublishesNoVotes()
	{
		var ordered = new RatingStrategy(new OrderedRating([RatingSource.RottenTomatoesCritics]));
		var critics = MediaRatings.From([new RottenTomatoesCriticsRating(87)]);

		await Assert
			.That(RuleTexts.Value(nameof(MediaQueryRow.Votes), Movie(critics, ordered), ordered))
			.IsEqualTo("no votes (Rotten Tomatoes® critics publishes none)");
	}

	[Test]
	public async Task Value_WhenTheRatingCameWithoutAVoteCount_SaysNoneCameWithIt()
	{
		var unvoted = MediaRatings.From([new TmdbRating(10.0f, new None())]);

		await Assert
			.That(RuleTexts.Value(nameof(MediaQueryRow.Votes), Movie(unvoted, Average), Average))
			.IsEqualTo("no votes (no vote count came with its rating)");
	}

	[Test]
	public async Task Value_WhenNothingRatesTheTitle_SaysNoRatingYet()
	{
		await Assert.That(RuleTexts.Value(nameof(MediaQueryRow.Votes), Movie(MediaRatings.None, Average), Average)).IsEqualTo("no rating yet");
	}

	[Test]
	public async Task Value_ForAnEpisodesRuleOnAMovie_SaysAMovieHasNone()
	{
		await Assert.That(RuleTexts.Value(nameof(MediaQueryRow.Episodes), Movie(LastSeen, Average), Average)).IsEqualTo("none (a movie)");
	}

	[Test]
	public async Task Written_ForABetweenRule_NamesBothBounds()
	{
		var between = new FilterCondition
		{
			Field = nameof(MediaQueryRow.Rating),
			Operator = FilterOperator.Between,
			Value = 8.0,
			ValueEnd = 9.0,
		};

		await Assert.That(RuleTexts.Written(between)).IsEqualTo(new WrittenRule("Rating", "between", "8 / 10 and 9 / 10"));
	}

	[Test]
	[MethodDataSource(nameof(Rules))]
	public async Task Written_ForEveryFieldAndOperator_ReadsAsTheEditorShowsIt(string field, FilterOperator op, string label)
	{
		var written = RuleTexts.Written(Condition(field, op));

		await Assert.That(written.Operator).IsEqualTo(label);
		await Assert.That(written.Value).IsNotEmpty();
	}

	[Test]
	[MethodDataSource(nameof(Properties))]
	public async Task Value_ForEveryField_ReadsTheRow(string property)
	{
		await Assert.That(RuleTexts.Value(property, FullShow(), Average)).IsNotEqualTo("not known yet").And.IsNotEqualTo("none");
	}

	public static IEnumerable<Func<(string Field, FilterOperator Op, string Label)>> Rules() =>
		FeedFields.All.SelectMany(field =>
			field.Operators.Select(offered => (Func<(string, FilterOperator, string)>)(() => (field.Property, offered.Value, offered.Text)))
		);

	public static IEnumerable<Func<string>> Properties() =>
		FeedFields
			.All.SelectMany(field => field.Total.Match(total => [field.Property, total], () => new[] { field.Property }))
			.Select(property => (Func<string>)(() => property));

	private static FilterCondition Condition(string field, FilterOperator op)
	{
		var sample = Sample(FeedFields.For(field), op);
		return new FilterCondition
		{
			Field = field,
			Operator = op,
			Value = sample,
			ValueEnd = op is FilterOperator.Between ? sample : null,
		};
	}

	private static object Sample(FeedField field, FilterOperator op)
	{
		return (field.Kind, op) switch
		{
			(FeedFieldKind.Choice, FilterOperator.In or FilterOperator.NotIn) => new[] { nameof(MediaType.Movie) },
			(FeedFieldKind.Choice, _) => nameof(MediaType.Movie),
			(FeedFieldKind.Text, _) => "piece",
			(FeedFieldKind.Number, _) => 5.0,
			(FeedFieldKind.Date, FilterOperator.Between) => new DateTime(2020, 1, 1),
			(FeedFieldKind.Date, _) => new RelativeDate(1, DateUnit.Years, Ahead: false),
			(FeedFieldKind.Genres, _) => new GenreMatch(["Anime"], []),
			(FeedFieldKind.Runtime, _) => new Duration(30, DurationUnit.Minutes),
		};
	}

	private static MediaQueryRow Movie(MediaRatings ratings, RatingStrategy strategy)
	{
		var details = new MovieDetails { Title = "Fight Club", Ratings = ratings };
		return MediaQueryRow.From(new MatchedMedia(new MediaIdentity(MediaId.New(), [TmdbId.From("550", MediaType.Movie)]), details), strategy, Everything);
	}

	private static MediaQueryRow FullShow()
	{
		var details = new ShowDetails
		{
			Title = "One Piece",
			Tagline = "Set sail for One Piece!",
			Summary = "A pirate crew sails in search of the One Piece.",
			Genres = ["Anime"],
			ReleaseDate = new DateOnly(1999, 10, 20),
			Seasons = 23,
			Episodes = 1219,
			RuntimePerEpisode = TimeSpan.FromMinutes(24),
			Ratings = LastSeen,
		};
		return MediaQueryRow.From(new MatchedMedia(new MediaIdentity(MediaId.New(), [TmdbId.From("37854", MediaType.Show)]), details), Average, Everything);
	}
}
