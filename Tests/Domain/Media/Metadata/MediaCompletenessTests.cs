using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using TestInfrastructure;

namespace Domain.Tests.Media.Metadata;

public class MediaCompletenessTests
{
	[Test]
	public async Task IsComplete_WhenAllFieldsPresent_IsTrue()
	{
		IPartialMedia movie = MediaFakes.CompletePartialMovie();

		await Assert.That(movie.IsComplete).IsTrue();
		await Assert.That(movie.Missing).IsEqualTo(MediaField.None);
	}

	[Test]
	public async Task Missing_WhenRuntimeMissing_NamesRuntime()
	{
		IPartialMedia movie = MediaFakes.CompletePartialMovie() with { Runtime = null };

		await Assert.That(movie.IsComplete).IsFalse();
		await Assert.That(movie.Missing).IsEqualTo(MediaField.Runtime);
	}

	[Test]
	public async Task Missing_WhenNoProviderHasRatedTheTitle_NamesRating()
	{
		IPartialMedia movie = MediaFakes.CompletePartialMovie() with { Rating = RatingSummary.None };

		await Assert.That(movie.IsComplete).IsFalse();
		await Assert.That(movie.Missing).IsEqualTo(MediaField.Rating);
	}

	[Test]
	public async Task Missing_WhenSeveralFieldsAreBlank_NamesEachOfThem()
	{
		IPartialMedia movie = MediaFakes.CompletePartialMovie() with { Genres = [], Tagline = " ", BackdropUrl = null };

		await Assert.That(movie.Missing).IsEqualTo(MediaField.Genres | MediaField.Tagline | MediaField.Backdrop);
	}

	[Test]
	public async Task Missing_ForAShowWithoutSeasonsOrEpisodes_NamesBoth()
	{
		IPartialMedia show = new PartialShow
		{
			Identity = new MediaIdentity(MediaId.From(Guid.NewGuid()), []),
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
	public async Task Confirming_KeepsTheFieldsOnThePartial()
	{
		IPartialMedia movie = MediaFakes.CompletePartialMovie() with { Tagline = null };

		await Assert
			.That(movie.Confirming(MediaField.Tagline))
			.IsOfUnionType<IdentifiedMedia, IPartialMedia>()
			.And.Multiple(async partial => await Assert.That(partial.ConfirmedMissing).IsEqualTo(MediaField.Tagline));
	}
}
