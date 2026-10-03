using Contracts;
using DB.Data.Media;
using DB.Data.Media.Metadata;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MediaMetadataFactoryTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private MediaMetadataFactory _testee = null!;

	[Before(Test)]
	public void Setup()
	{
		var dateTimeProvider = IDateTimeProvider.Mock();
		dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);
		_testee = new MediaMetadataFactory(dateTimeProvider.Object);
	}

	private async Task<MediaId> SeedMediaAsync(IEnumerable<IExternalMediaId> ids)
	{
		var media = new MediaRow();
		await fixture.Context.ExternalMediaIds.AddRangeAsync(ids.Select(id => id.ToExternalIdEntity(media)));
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return MediaId.From(media.Id);
	}

	[Test]
	public async Task BuildFrom_WhenMovieDetails_CopiesAllFields()
	{
		var details = MovieDetails();

		var entity = (MovieMetadataRow)_testee.BuildFrom(Movie(MediaId.From(Guid.NewGuid()), [TmdbId.From("100", MediaType.Movie)], details), Guid.NewGuid());

		using var _ = Assert.Multiple();
		await Assert.That(entity.Title).IsEqualTo(details.Title);
		await Assert.That(entity.Tagline).IsEqualTo(details.Tagline);
		await Assert.That(entity.Summary).IsEqualTo(details.Summary);
		await Assert.That(entity.PosterUrl).IsEqualTo(details.PosterUrl);
		await Assert.That(entity.BackdropUrl).IsEqualTo(details.BackdropUrl);
		await Assert.That(entity.ReleaseDate).IsEqualTo(details.ReleaseDate);
		await Assert.That(entity.Genres).IsEquivalentTo(details.Genres);
		await Assert.That(entity.Runtime).IsEqualTo(details.Runtime);
	}

	[Test]
	public async Task BuildFrom_WhenShowDetails_CopiesAllFields()
	{
		var details = ShowDetails();

		var entity = (ShowMetadataRow)_testee.BuildFrom(Show(MediaId.From(Guid.NewGuid()), [TmdbId.From("200", MediaType.Show)], details), Guid.NewGuid());

		using var _ = Assert.Multiple();
		await Assert.That(entity.Title).IsEqualTo(details.Title);
		await Assert.That(entity.Seasons).IsEqualTo(details.Seasons);
		await Assert.That(entity.Episodes).IsEqualTo(details.Episodes);
		await Assert.That(entity.RuntimePerEpisode).IsEqualTo(details.RuntimePerEpisode);
	}

	[Test]
	public async Task BuildFrom_WhenDetailsHaveRatings_BuildsRatings()
	{
		var details = MovieDetails() with { Rating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 8.4f, 1234)]) };

		var entity = (MovieMetadataRow)_testee.BuildFrom(Movie(MediaId.From(Guid.NewGuid()), [TmdbId.From("110", MediaType.Movie)], details), Guid.NewGuid());

		await Assert.That(entity.Ratings).Count().IsEqualTo(1);
		await Assert.That(entity.Ratings[0].Source).IsEqualTo(MediaProvider.Tmdb);
		await Assert.That(entity.Ratings[0].NormalizedValue).IsEqualTo(8.4f);
	}

	[Test]
	public async Task Update_WhenSameType_MutatesScalarsInPlace()
	{
		var plexId = PlexId.From("plex-300", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var existing = await PersistAsync(Movie(mediaId, [plexId], MovieDetails() with { Title = "Original" }), mediaId);

		_testee.Update(existing, Movie(mediaId, [plexId], MovieDetails() with { Title = "Updated", Runtime = TimeSpan.FromMinutes(99) }));

		await Assert.That(existing.Title).IsEqualTo("Updated");
		await Assert.That(((MovieMetadataRow)existing).Runtime).IsEqualTo(TimeSpan.FromMinutes(99));
	}

	[Test]
	public async Task Update_WhenSameType_ReplacesRatings()
	{
		var plexId = PlexId.From("plex-310", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var existing = await PersistAsync(
			Movie(mediaId, [plexId], MovieDetails() with { Rating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 5.0f, 10)]) }),
			mediaId
		);

		_testee.Update(existing, Movie(mediaId, [plexId], MovieDetails() with { Rating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 9.0f, 9999)]) }));
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		var stored = await fixture.Context.MediaRatings.AsNoTracking().SingleAsync();
		using var _ = Assert.Multiple();
		await Assert.That(stored.Source).IsEqualTo(MediaProvider.Tmdb);
		await Assert.That(stored.NormalizedValue).IsEqualTo(9.0f);
		await Assert.That(stored.VoteCount).IsEqualTo(9999);
	}

	[Test]
	public async Task Update_WhenNewRatingsAreEmpty_RemovesExistingRatings()
	{
		var plexId = PlexId.From("plex-315", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var existing = await PersistAsync(
			Movie(mediaId, [plexId], MovieDetails() with { Rating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 5.0f, 10)]) }),
			mediaId
		);

		_testee.Update(existing, Movie(mediaId, [plexId], new MovieDetails { Title = "Title" }));
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		await Assert.That(await fixture.Context.MediaRatings.AsNoTracking().AnyAsync()).IsFalse();
	}

	[Test]
	public async Task Update_WhenTypeMismatch_Throws()
	{
		var plexId = PlexId.From("plex-320", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var existing = await PersistAsync(Movie(mediaId, [plexId], MovieDetails()), mediaId);
		var show = Show(MediaId.From(Guid.NewGuid()), [TmdbId.From("321", MediaType.Show)], ShowDetails());

		await Assert.That(() => _testee.Update(existing, show)).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task ToStored_WhenMovieEntity_ReadsBackTheMovieItWrote()
	{
		var plexId = PlexId.From("plex-400", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var details = MovieDetails() with { Title = "Movie", Runtime = TimeSpan.FromMinutes(110) };
		var entity = await PersistAsync(Movie(mediaId, [plexId], details), mediaId);

		var stored = _testee.ToStored(entity, new MediaIdentity(mediaId, [plexId]));

		await Assert.That(stored).IsEqualTo(Movie(mediaId, [plexId], details));
	}

	[Test]
	public async Task ToStored_WhenShowEntity_ReadsBackTheShowItWrote()
	{
		var plexId = PlexId.From("plex-410", MediaType.Show);
		var mediaId = await SeedMediaAsync([plexId]);
		var entity = await PersistAsync(Show(mediaId, [plexId], ShowDetails()), mediaId);

		var stored = _testee.ToStored(entity, new MediaIdentity(mediaId, [plexId]));

		await Assert.That(stored).IsEqualTo(Show(mediaId, [plexId], ShowDetails()));
	}

	[Test]
	public async Task ToStored_PassesThroughExternalsAndRatings()
	{
		var plexId = PlexId.From("plex-420", MediaType.Movie);
		var imdbId = ImdbId.From("tt420", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId, imdbId]);
		var entity = await PersistAsync(
			Movie(mediaId, [plexId, imdbId], MovieDetails() with { Rating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 7.5f, 500)]) }),
			mediaId
		);

		var stored = _testee.ToStored(entity, new MediaIdentity(mediaId, [plexId, imdbId]));

		using var _ = Assert.Multiple();
		await Assert.That(stored.Identity.Externals).Contains(imdbId);
		await Assert.That(stored.Details.Rating.Sources.Count).IsEqualTo(1);
		await Assert.That(stored.Details.Rating.Sources.Single().Source).IsEqualTo(MediaProvider.Tmdb);
	}

	[Test]
	public async Task BuildFrom_WhenSomeFieldsAreMissing_WritesTheValuesItHas()
	{
		var plexId = PlexId.From("plex-420", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var details = new MovieDetails
		{
			Title = "Partial Title",
			Tagline = "Partial Tagline",
			Summary = "Partial Summary",
			PosterUrl = "https://example.com/partial-poster.jpg",
			ReleaseDate = new DateOnly(2019, 5, 4),
			Runtime = TimeSpan.FromMinutes(95),
		};

		var entity = await PersistAsync(Movie(mediaId, [plexId], details), mediaId);

		using var _ = Assert.Multiple();
		await Assert.That(entity.Title).IsEqualTo("Partial Title");
		await Assert.That(entity.Tagline).IsEqualTo("Partial Tagline");
		await Assert.That(entity.Summary).IsEqualTo("Partial Summary");
		await Assert.That(entity.PosterUrl).IsEqualTo("https://example.com/partial-poster.jpg");
		await Assert.That(entity.ReleaseDate).IsEqualTo(new DateOnly(2019, 5, 4));
		await Assert.That(((MovieMetadataRow)entity).Runtime).IsEqualTo(TimeSpan.FromMinutes(95));
	}

	[Test]
	public async Task ToStored_WhenTheMovieEntityHoldsSentinels_ReadsThemBackAsMissing()
	{
		var plexId = PlexId.From("plex-421", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var entity = await PersistAsync(Movie(mediaId, [plexId], new MovieDetails { Title = "Only a title" }), mediaId);

		var stored = _testee.ToStored(entity, new MediaIdentity(mediaId, [plexId]));

		await Assert.That(stored.Details).IsEqualTo(new MovieDetails { Title = "Only a title" });
	}

	[Test]
	public async Task ToStored_WhenTheShowEntityHoldsSentinels_ReadsThemBackAsMissing()
	{
		var plexId = PlexId.From("plex-422", MediaType.Show);
		var mediaId = await SeedMediaAsync([plexId]);
		var entity = await PersistAsync(Show(mediaId, [plexId], new ShowDetails { Title = "Only a title" }), mediaId);

		var stored = _testee.ToStored(entity, new MediaIdentity(mediaId, [plexId]));

		await Assert.That(stored.Details).IsEqualTo(new ShowDetails { Title = "Only a title" });
	}

	[Test]
	public async Task BuildFrom_WhenAShowHasCounts_WritesThem()
	{
		var plexId = PlexId.From("plex-423", MediaType.Show);
		var mediaId = await SeedMediaAsync([plexId]);
		var details = new ShowDetails
		{
			Title = "Partial Show",
			Seasons = 4,
			Episodes = 40,
			RuntimePerEpisode = TimeSpan.FromMinutes(22),
		};

		var entity = (ShowMetadataRow)await PersistAsync(Show(mediaId, [plexId], details), mediaId);

		using var _ = Assert.Multiple();
		await Assert.That(entity.Seasons).IsEqualTo(4);
		await Assert.That(entity.Episodes).IsEqualTo(40);
		await Assert.That(entity.RuntimePerEpisode).IsEqualTo(TimeSpan.FromMinutes(22));
	}

	private async Task<MediaMetadataRow> PersistAsync(IdentifiedMedia media, MediaId mediaId)
	{
		var entity = _testee.BuildFrom(media, mediaId.Value);
		fixture.Context.MediaMetadata.Add(entity);
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		return await fixture.Context.MediaMetadata.Include(m => m.Ratings).SingleAsync(m => m.MediaId == mediaId.Value);
	}

	private static IdentifiedMedia Movie(MediaId mediaId, IExternalMediaId[] externals, MovieDetails details)
	{
		return new(new MediaIdentity(mediaId, [.. externals]), details);
	}

	private static IdentifiedMedia Show(MediaId mediaId, IExternalMediaId[] externals, ShowDetails details)
	{
		return new(new MediaIdentity(mediaId, [.. externals]), details);
	}

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
			Rating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 7.5f, 100)]),
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
			Rating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 7.5f, 100)]),
			Seasons = 3,
			Episodes = 30,
			RuntimePerEpisode = TimeSpan.FromMinutes(45),
		};
	}
}
