using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using UI.Shared.Media;

namespace UI.Tests.Media;

public class MediaPresentationTests
{
	[Test]
	public async Task Id_WhenTheMediaIsUnmatched_Throws()
	{
		MediaLookup media = Unmatched();

		await Assert.That(() => media.Id).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task Id_WhenTheMediaIsIdentified_ReturnsTheIdentityId()
	{
		var movie = Identified();
		MediaLookup media = (IdentifiedMedia)movie;

		await Assert.That(media.Id).IsEqualTo(movie.Identity.Id);
	}

	[Test]
	public async Task Key_WhenTheMediaIsUnmatched_FallsBackToTheExternalIds()
	{
		MediaLookup media = Unmatched();

		await Assert.That(media.Key).IsEqualTo("Tmdb:27205");
	}

	[Test]
	public async Task Key_WhenTheMediaIsIdentified_UsesTheMediaId()
	{
		var movie = Identified();
		MediaLookup media = (IdentifiedMedia)movie;

		await Assert.That(media.Key).IsEqualTo(movie.Identity.Id.Value.ToString());
	}

	[Test]
	public async Task Title_WhenMissing_IsEmptyRatherThanNull()
	{
		MediaLookup media = Unmatched();

		await Assert.That(media.Title).IsEqualTo(string.Empty);
		await Assert.That(media.Summary).IsEqualTo(string.Empty);
		await Assert.That(media.PosterUrl).IsEqualTo(string.Empty);
		await Assert.That(media.BackdropUrl).IsEqualTo(string.Empty);
	}

	[Test]
	public async Task ReleaseDate_WhenMissing_IsMinValue()
	{
		MediaLookup media = Unmatched();

		await Assert.That(media.ReleaseDate).IsEqualTo(DateOnly.MinValue);
	}

	[Test]
	public async Task Tagline_WhenTheMediaIsUnmatched_IsEmpty()
	{
		MediaLookup media = Unmatched() with { Details = new MovieDetails { Title = "Inception" } };

		await Assert.That(media.Tagline).IsEqualTo(string.Empty);
	}

	[Test]
	public async Task Seasons_WhenTheMediaIsUnmatched_AreUnknown()
	{
		MediaLookup media = Unmatched();

		await Assert.That(media.Seasons).IsNull();
		await Assert.That(media.Episodes).IsNull();
		await Assert.That(media.Runtime).IsNull();
	}

	[Test]
	public async Task MediaType_WhenTheMediaIsUnmatched_ComesFromItsDetails()
	{
		MediaLookup media = new UnmatchedMedia([], new ShowDetails());

		await Assert.That(media.MediaType).IsEqualTo(MediaType.Show);
	}

	private static UnmatchedMedia Unmatched() => new([TmdbId.From("27205", MediaType.Movie)], new MovieDetails());

	private static IdentifiedMedia Identified()
	{
		return new(
			new MediaIdentity(MediaId.From(Guid.NewGuid()), [TmdbId.From("27205", MediaType.Movie)]),
			new MovieDetails
			{
				Title = "Inception",
				Tagline = "Your mind is the scene of the crime",
				Summary = "A thief who steals corporate secrets.",
				PosterUrl = "/poster.jpg",
				BackdropUrl = "/backdrop.jpg",
				ReleaseDate = new DateOnly(2010, 7, 16),
				Genres = ["Action"],
				Rating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 7.5f, 100)]),
				Runtime = TimeSpan.FromMinutes(148),
			}
		);
	}
}
