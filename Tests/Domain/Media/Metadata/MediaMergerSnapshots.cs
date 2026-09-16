using Domain.Media;
using Domain.Media.Metadata;

namespace Domain.Tests.Media.Metadata;

public class MediaMergerSnapshots
{
	private static readonly MediaId Id = MediaId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));

	[Test]
	public Task MergeWith_WhenATmdbEnrichmentFollowsAPlexBase_ProducesTheExpectedMovie()
	{
		var plex = new PartialMovie
		{
			Identity = new MediaIdentity(Id, [PlexId.From("550", MediaType.Movie), TmdbId.From("550", MediaType.Movie)]),
			Title = "Fight Club",
			Tagline = "Mischief. Mayhem. Soap.",
			Summary = "An insomniac office worker and a soap maker form an underground fight club.",
			PosterUrl = "/poster.jpg",
			BackdropUrl = "/backdrop.jpg",
			ReleaseDate = new DateOnly(1999, 10, 15),
			Runtime = TimeSpan.FromMinutes(139),
			Rating = RatingSummary.None,
		};

		var tmdb = new PartialMovie
		{
			Identity = new MediaIdentity(Id, [TmdbId.From("550", MediaType.Movie), ImdbId.From("tt0137523", MediaType.Movie)]),
			Genres = ["Drama", "Thriller"],
			Rating = RatingSummary.None,
		};

		var merged = plex.MergeWith(tmdb);

		return Verify(merged);
	}
}
