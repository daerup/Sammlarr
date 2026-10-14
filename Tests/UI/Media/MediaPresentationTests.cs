using Domain.Media;
using UI.Shared.Media;

namespace UI.Tests.Media;

public class MediaPresentationTests
{
	[Test]
	public async Task Key_OfAnUnmatchedTitle_FallsBackToTheExternalIds()
	{
		MediaMatchResult media = Unmatched();

		await Assert.That(media.Key).IsEqualTo("Tmdb:27205");
	}

	[Test]
	public async Task Key_OfAMatchedTitle_UsesTheMediaIdWhetherReadAsAMatchResultOrNot()
	{
		var movie = Matched();
		MediaMatchResult media = movie;

		await Assert.That(media.Key).IsEqualTo(movie.Identity.Id.Value.ToString());
		await Assert.That(movie.Key).IsEqualTo(movie.Identity.Id.Value.ToString());
	}

	private static UnmatchedMedia Unmatched() => new([TmdbId.From("27205", MediaType.Movie)], new MovieDetails());

	private static MatchedMedia Matched()
	{
		return new(
			new MediaIdentity(MediaId.New(), [TmdbId.From("27205", MediaType.Movie)]),
			new MovieDetails
			{
				Title = "Inception",
				Tagline = "Your mind is the scene of the crime",
				Summary = "A thief who steals corporate secrets.",
				PosterUrl = "/poster.jpg",
				BackdropUrl = "/backdrop.jpg",
				ReleaseDate = new DateOnly(2010, 7, 16),
				Genres = ["Action"],
				Ratings = MediaRatings.From([new TmdbRating(7.5f, Some.Of(100))]),
				Runtime = TimeSpan.FromMinutes(148),
			}
		);
	}
}
