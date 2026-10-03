using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using UI.Shared.Media;

namespace UI.Tests.Media;

public class MediaPresentationTests
{
	[Test]
	public async Task Key_OfAnUnmatchedTitle_FallsBackToTheExternalIds()
	{
		MediaLookup media = Unmatched();

		await Assert.That(media.Key).IsEqualTo("Tmdb:27205");
	}

	[Test]
	public async Task Key_OfAnIdentifiedTitle_UsesTheMediaId()
	{
		var movie = Identified();
		MediaLookup media = movie;

		await Assert.That(media.Key).IsEqualTo(movie.Identity.Id.Value.ToString());
	}

	[Test]
	public async Task Key_OfAnIdentifiedTitle_IsTheSameWhetherReadAsALookupOrNot()
	{
		var movie = Identified();
		MediaLookup media = movie;

		await Assert.That(movie.Key).IsEqualTo(media.Key);
	}

	[Test]
	public async Task Year_WhenReleased_IsTheReleaseYear()
	{
		await Assert.That(Identified().Details.Year).IsEqualTo(2010);
	}

	[Test]
	public async Task Year_WhenTheReleaseDateIsUnknown_IsNull()
	{
		await Assert.That(Unmatched().Details.Year).IsNull();
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
