using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;

namespace Domain.Tests.Media.Metadata;

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
	public async Task Missing_WhenNoProviderHasRatedTheTitle_NamesRating()
	{
		var movie = MediaFakes.CompleteMovieDetails() with { Rating = RatingSummary.None };

		await Assert.That(movie.Missing).IsEqualTo(MediaField.Rating);
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
			Rating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 8.7f)]),
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
				MediaField.Tagline | MediaField.Summary | MediaField.Poster | MediaField.Backdrop | MediaField.ReleaseDate | MediaField.Genres | MediaField.Rating
			);
	}
}
