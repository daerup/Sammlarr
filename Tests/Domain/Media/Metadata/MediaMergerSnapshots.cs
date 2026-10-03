using Domain.Media;
using Domain.Media.Metadata;

namespace Domain.Tests.Media.Metadata;

public class MediaMergerSnapshots
{
	[Test]
	public Task FillFrom_WhenATmdbEnrichmentFollowsAPlexBase_ProducesTheExpectedMovie()
	{
		var plex = new MovieDetails
		{
			Title = "Fight Club",
			Tagline = "Mischief. Mayhem. Soap.",
			Summary = "An insomniac office worker and a soap maker form an underground fight club.",
			PosterUrl = "/poster.jpg",
			BackdropUrl = "/backdrop.jpg",
			ReleaseDate = new DateOnly(1999, 10, 15),
			Runtime = TimeSpan.FromMinutes(139),
		};

		var tmdb = new MovieDetails { Genres = ["Drama", "Thriller"] };

		var merged = plex.FillFrom(tmdb);

		return Verify(merged);
	}
}
