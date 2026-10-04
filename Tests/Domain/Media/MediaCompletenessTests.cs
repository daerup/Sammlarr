using Domain.Media;

namespace Domain.Tests.Media;

public class MediaCompletenessTests
{
	[Test]
	public async Task Missing_WhenAllFieldsPresent_IsNone()
	{
		var movie = MediaFakes.CompleteMovieDetails();

		await Assert.That(movie.Missing).IsEqualTo(MediaField.None);
	}

	[Test]
	public async Task Missing_WhenRuntimeMissing_NamesRuntime()
	{
		var movie = MediaFakes.CompleteMovieDetails() with { Runtime = null };

		await Assert.That(movie.Missing).IsEqualTo(MediaField.Runtime);
	}

	[Test]
	public async Task Missing_WhenNoProviderHasRatedTheTitle_NamesEveryRatingSource()
	{
		var movie = MediaFakes.CompleteMovieDetails() with { Ratings = MediaRatings.None };

		await Assert.That(movie.Missing).IsEqualTo(MediaField.Ratings);
	}

	[Test]
	public async Task Missing_WhenOnlyTmdbHasRatedTheTitle_NamesTheOtherSources()
	{
		var movie = MediaFakes.CompleteMovieDetails() with { Ratings = MediaRatings.From([new TmdbRating(7.4f, Some.Of(900))]) };

		await Assert.That(movie.Missing).IsEqualTo(MediaField.ImdbRating | MediaField.RottenTomatoesCriticsRating | MediaField.RottenTomatoesAudienceRating);
	}

	[Test]
	public async Task Missing_WhenARatingFromASourceThatPublishesVotesHasNone_StillNamesIt()
	{
		var movie = MediaFakes.CompleteMovieDetails() with
		{
			Ratings = MediaRatings.From([
				new TmdbRating(8.9f, new None()),
				new ImdbRating(7.6f, Some.Of(303)),
				new RottenTomatoesCriticsRating(87),
				new RottenTomatoesAudienceRating(91),
			]),
		};

		await Assert.That(movie.Missing).IsEqualTo(MediaField.TmdbRating);
	}

	[Test]
	public async Task Missing_WhenSeveralFieldsAreUnknown_NamesEachOfThem()
	{
		var movie = MediaFakes.CompleteMovieDetails() with { Genres = [], Tagline = null, BackdropUrl = null };

		await Assert.That(movie.Missing).IsEqualTo(MediaField.Genres | MediaField.Tagline | MediaField.Backdrop);
	}

	[Test]
	public async Task Missing_ForAShowWithoutSeasonsOrEpisodes_NamesBoth()
	{
		var show = new ShowDetails
		{
			Title = "Severance",
			Tagline = "Work is life.",
			Summary = "Office workers split their memories.",
			PosterUrl = "poster",
			BackdropUrl = "backdrop",
			ReleaseDate = new DateOnly(2022, 2, 18),
			Genres = ["Drama"],
			Ratings = MediaFakes.EverySourceRating(),
			RuntimePerEpisode = TimeSpan.FromMinutes(50),
		};

		await Assert.That(show.Missing).IsEqualTo(MediaField.Seasons | MediaField.Episodes);
	}

	[Test]
	public async Task Missing_ForAnUnknownTitle_NamesOnlyTheCommonFields()
	{
		var unknown = new UnknownDetails { Title = "Christopher Nolan" };

		await Assert
			.That(unknown.Missing)
			.IsEqualTo(
				MediaField.Tagline | MediaField.Summary | MediaField.Poster | MediaField.Backdrop | MediaField.ReleaseDate | MediaField.Genres | MediaField.Ratings
			);
	}
}
