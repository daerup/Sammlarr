using DB.Data.Media;
using Domain.Media;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MediaDetailsFactoryTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	private static readonly DateTimeOffset RefreshedAt = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
	private static readonly ProviderAnswer TmdbLackedTheTagline = new(RemoteService.Tmdb, MediaField.Tagline, RefreshedAt);
	private static readonly ProviderAnswer PlexLackedTheAudience = new(
		RemoteService.Plex,
		MediaFields.Rated([RatingSource.RottenTomatoesAudience]),
		RefreshedAt.AddDays(-3)
	);

	private readonly MediaDetailsFactory _testee = new();

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Test]
	public async Task ToCached_WhenMovieEntity_ReadsBackWhatItWrote()
	{
		var plexId = PlexId.From("plex-400", MediaType.Movie);
		var mediaId = await fixture.SeedMediaAsync([plexId]);
		var written = new CachedDetails(
			MovieDetails() with
			{
				Title = "Movie",
				Runtime = TimeSpan.FromMinutes(110),
			},
			RefreshedAt,
			RefreshedAt.AddHours(1),
			[TmdbLackedTheTagline, PlexLackedTheAudience]
		);
		var entity = await PersistAsync(written, mediaId);

		await Assert.That(_testee.ToCached(entity)).IsEqualTo(written);
	}

	[Test]
	public async Task ToCached_WhenShowEntity_ReadsBackWhatItWrote()
	{
		var plexId = PlexId.From("plex-410", MediaType.Show);
		var mediaId = await fixture.SeedMediaAsync([plexId]);
		var entity = await PersistAsync(Cached(ShowDetails()), mediaId);

		await Assert.That(_testee.ToCached(entity)).IsEqualTo(Cached(ShowDetails()));
	}

	[Test]
	public async Task ToCached_WhenAMovieRowHasOnlyATitle_ReadsTheRestAsUnknown()
	{
		var plexId = PlexId.From("plex-421", MediaType.Movie);
		var mediaId = await fixture.SeedMediaAsync([plexId]);
		var entity = await PersistAsync(Cached(new MovieDetails { Title = "Only a title" }), mediaId);

		await Assert.That(_testee.ToCached(entity).Details).IsEqualTo(new MovieDetails { Title = "Only a title" });
	}

	[Test]
	public async Task ToCached_WhenAShowRowHasOnlyATitle_ReadsTheRestAsUnknown()
	{
		var plexId = PlexId.From("plex-422", MediaType.Show);
		var mediaId = await fixture.SeedMediaAsync([plexId]);
		var entity = await PersistAsync(Cached(new ShowDetails { Title = "Only a title" }), mediaId);

		await Assert.That(_testee.ToCached(entity).Details).IsEqualTo(new ShowDetails { Title = "Only a title" });
	}

	private async Task<MediaDetailsRow> PersistAsync(CachedDetails cached, MediaId mediaId)
	{
		var entity = _testee.BuildFrom(cached, mediaId.Value);
		fixture.Context.MediaDetails.Add(entity);
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		return await fixture
			.Context.MediaDetails.Include(m => m.Ratings)
			.Include(m => m.Answers)
			.Include(m => m.PageLinks)
			.SingleAsync(m => m.MediaId == mediaId.Value);
	}

	private static CachedDetails Cached(MediaDetails details) => new(details, RefreshedAt, RefreshedAt, []);

	private static MovieDetails MovieDetails()
	{
		return new()
		{
			Title = "Title",
			Tagline = "Tagline",
			Summary = "Summary",
			PosterUrl = "https://example.com/poster.jpg",
			BackdropUrl = "https://example.com/backdrop.jpg",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Action"],
			Ratings = MediaRatings.From([new TmdbRating(7.5f, Some.Of(100))]),
			Runtime = TimeSpan.FromMinutes(120),
		};
	}

	private static ShowDetails ShowDetails()
	{
		return new()
		{
			Title = "Show Title",
			Tagline = "Show Tagline",
			Summary = "Show Summary",
			PosterUrl = "https://example.com/show-poster.jpg",
			BackdropUrl = "https://example.com/show-backdrop.jpg",
			ReleaseDate = new DateOnly(2020, 1, 1),
			Genres = ["Drama"],
			Ratings = MediaRatings.From([new TmdbRating(7.5f, Some.Of(100))]),
			Seasons = 3,
			Episodes = 30,
			RuntimePerEpisode = TimeSpan.FromMinutes(45),
		};
	}
}
