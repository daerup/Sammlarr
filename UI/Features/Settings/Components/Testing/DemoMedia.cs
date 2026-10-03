using Domain.Media;
using Domain.Providers;

namespace UI.Features.Settings.Components.Testing;

internal static class DemoMedia
{
	public static readonly MatchedMedia Movie = new(
		new MediaIdentity(
			MediaId.From(new Guid("11111111-1111-1111-1111-111111111111")),
			[TmdbId.From("687163", MediaType.Movie), ImdbId.From("tt12042730", MediaType.Movie), PlexId.From("5d776be07bb7e9001e73bb0f", MediaType.Movie)]
		),
		new MovieDetails
		{
			Title = "Project Hail Mary",
			Tagline = "One man. One ship. One shot to save humanity.",
			Summary =
				"A lone astronaut wakes far from home with no memory of how he got there, and must piece together a desperate mission to keep the sun from dying.",
			PosterUrl = "https://image.tmdb.org/t/p/w500/1hJdqwEDES96CRZCaiOVuqWLJFX.jpg",
			BackdropUrl = "https://image.tmdb.org/t/p/w780/qL4dBGLkWOVP5vImN7EEoqJQYd8.jpg",
			ReleaseDate = new DateOnly(2026, 3, 20),
			Genres = ["Science Fiction", "Adventure"],
			Rating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 8.4f)]),
			Runtime = TimeSpan.FromMinutes(150),
		}
	);

	public static readonly MatchedMedia Show = new(
		new MediaIdentity(
			MediaId.From(new Guid("22222222-2222-2222-2222-222222222222")),
			[
				TmdbId.From("1396", MediaType.Show),
				ImdbId.From("tt0903747", MediaType.Show),
				TvdbId.From("81189", MediaType.Show),
				PlexId.From("5d9c081b166f9e0020c6f1e0", MediaType.Show),
			]
		),
		new ShowDetails
		{
			Title = "Breaking Bad",
			Tagline = "A quiet life takes a darker turn.",
			Summary =
				"A high-school chemistry teacher turns to making illicit product to secure his family's future, and is slowly reshaped by the choices he makes.",
			PosterUrl = "https://image.tmdb.org/t/p/w500/anFx9aTOOYqgS3v7x3R84Kz67ly.jpg",
			BackdropUrl = "https://image.tmdb.org/t/p/w780/tsRy63Mu5cu8etL1X7ZLyf7UP1M.jpg",
			ReleaseDate = new DateOnly(2008, 1, 20),
			Genres = ["Drama", "Crime"],
			Rating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 8.9f)]),
			Seasons = 5,
			Episodes = 62,
			RuntimePerEpisode = TimeSpan.FromMinutes(47),
		}
	);

	public static readonly MatchedMedia Gohan = new(
		new MediaIdentity(MediaId.From(new Guid("33333333-3333-3333-3333-333333333333")), []),
		new MovieDetails
		{
			Title = "Gohan",
			Tagline = "One stray dog, three owners, comes the love that lasts fur-ever.",
			Summary =
				"The bonds between humans and animals following a stray dog named Gohan as it moves through life with temporary owners over a decade, through good times and bad times, joy and sorrow, hellos and goodbyes.",
			PosterUrl = "https://image.tmdb.org/t/p/w500/nVq1Dn88NzVIVTDpGZeP7fxpLa1.jpg",
			BackdropUrl = "https://image.tmdb.org/t/p/w780/edS1AlGAXopVq0L2voGOooYcE1S.jpg",
			ReleaseDate = new DateOnly(2026, 4, 2),
			Genres = ["Drama", "Family"],
			Rating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 7.1f)]),
			Runtime = TimeSpan.FromMinutes(116),
		}
	);

	public static readonly MatchedMedia YellowMirror = new(
		new MediaIdentity(MediaId.From(new Guid("44444444-4444-4444-4444-444444444444")), []),
		new MovieDetails
		{
			Title = "Yellow Mirror",
			Summary = "A defective lamp reveals a heartbreaking truth about what Homer believes is reality, and an AI-powered tablet befriends and controls Maggie.",
			PosterUrl = "https://image.tmdb.org/t/p/w500/1zdGvJAuuXC7dA3eV61OtUJNyjQ.jpg",
			BackdropUrl = "https://image.tmdb.org/t/p/w780/rFjlsx5IygrHmBqr0KsVolAHhy7.jpg",
			ReleaseDate = new DateOnly(2026, 8, 26),
			Genres = ["Animation", "Comedy"],
			Rating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 8.1f)]),
			Runtime = TimeSpan.FromMinutes(44),
		}
	);

	public static readonly MatchedMedia WolfBeach = new(
		new MediaIdentity(MediaId.From(new Guid("55555555-5555-5555-5555-555555555555")), []),
		new MovieDetails
		{
			Title = "Wolf Beach",
			Tagline = "Two strangers with lots of sea in the background.",
			Summary =
				"Manu works at a beach bar. Klaus won't leave the last beach chair. What seems like a clash between opposites turns suspicious when Manu doubts Klaus's presence is coincidental.",
			PosterUrl = "https://image.tmdb.org/t/p/w500/cBUeTWemlBG9b22TY2qMibtfgaA.jpg",
			BackdropUrl = "https://image.tmdb.org/t/p/w780/pOGqAA6AOOKLRWOfYvRAdDP0vOl.jpg",
			ReleaseDate = new DateOnly(2025, 12, 5),
			Genres = ["Comedy", "Thriller"],
			Rating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 5.8f)]),
			Runtime = TimeSpan.FromMinutes(98),
		}
	);

	public static readonly MatchedMedia NoHitWonder = new(
		new MediaIdentity(MediaId.From(new Guid("66666666-6666-6666-6666-666666666666")), []),
		new MovieDetails
		{
			Title = "No Hit Wonder",
			Summary = "Singer Daniel has hit rock bottom. After a failed suicide attempt, the former one-hit wonder finds himself stuck in a psychiatric ward.",
			PosterUrl = "https://image.tmdb.org/t/p/w500/91Nc6a2utk3K3wtosrkyQ5z3sxM.jpg",
			BackdropUrl = "https://image.tmdb.org/t/p/w780/hDp8Itlhln0aBjlHXVIsUsxiRaa.jpg",
			ReleaseDate = new DateOnly(2025, 10, 30),
			Genres = ["Comedy", "Drama"],
			Rating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 6.8f)]),
			Runtime = TimeSpan.FromMinutes(116),
		}
	);

	public static readonly UnmatchedMedia Unknown = new(
		[TmdbId.From("999999", MediaType.Movie)],
		new MovieDetails { Title = "Unmatched Title", ReleaseDate = new DateOnly(2024, 1, 1) }
	);
}
