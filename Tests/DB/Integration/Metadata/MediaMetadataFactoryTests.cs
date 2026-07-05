using Contracts;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using DB.Data.Media;
using DB.Data.Metadata;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration.Metadata;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class MediaMetadataFactoryTests(SQLiteFixture fixture) : IAsyncDisposable
{
	private MediaMetadataFactory _testee = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

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
		var details = CreateMovieDetails(MediaId.From(Guid.NewGuid()), [TmdbId.From("100", MediaType.Movie)]);

		var entity = (MovieMetadataRow)_testee.BuildFrom(details, Guid.NewGuid());

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
		var details = CreateShowDetails(MediaId.From(Guid.NewGuid()), [TmdbId.From("200", MediaType.Show)]);

		var entity = (ShowMetadataRow)_testee.BuildFrom(details, Guid.NewGuid());

		using var _ = Assert.Multiple();
		await Assert.That(entity.Title).IsEqualTo(details.Title);
		await Assert.That(entity.Seasons).IsEqualTo(details.Seasons);
		await Assert.That(entity.Episodes).IsEqualTo(details.Episodes);
		await Assert.That(entity.RuntimePerEpisode).IsEqualTo(details.RuntimePerEpisode);
	}

	[Test]
	public async Task BuildFrom_WhenDetailsHaveRatings_BuildsRatings()
	{
		var details = CreateMovieDetails(MediaId.From(Guid.NewGuid()), [TmdbId.From("110", MediaType.Movie)]) with
		{
			Rating = RatingSummary.From([new MediaRating(RatingProvider.Tmdb, 8.4f, 1234)]),
		};

		var entity = (MovieMetadataRow)_testee.BuildFrom(details, Guid.NewGuid());

		await Assert.That(entity.Ratings).Count().IsEqualTo(1);
		await Assert.That(entity.Ratings[0].Source).IsEqualTo(RatingProvider.Tmdb);
		await Assert.That(entity.Ratings[0].NormalizedValue).IsEqualTo(8.4f);
	}

	[Test]
	public async Task Update_WhenSameType_MutatesScalarsInPlace()
	{
		var plexId = PlexId.From("plex-300", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var existing = await PersistAsync(CreateMovieDetails(mediaId, [plexId]) with { Title = "Original" }, mediaId);

		_testee.Update(existing, CreateMovieDetails(mediaId, [plexId]) with { Title = "Updated", Runtime = TimeSpan.FromMinutes(99) });

		await Assert.That(existing.Title).IsEqualTo("Updated");
		await Assert.That(((MovieMetadataRow)existing).Runtime).IsEqualTo(TimeSpan.FromMinutes(99));
	}

	[Test]
	public async Task Update_WhenSameType_ReplacesRatings()
	{
		var plexId = PlexId.From("plex-310", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var existing = await PersistAsync(
			CreateMovieDetails(mediaId, [plexId]) with
			{
				Rating = RatingSummary.From([new MediaRating(RatingProvider.Tmdb, 5.0f, 10)]),
			},
			mediaId
		);

		_testee.Update(existing, CreateMovieDetails(mediaId, [plexId]) with { Rating = RatingSummary.From([new MediaRating(RatingProvider.Tmdb, 9.0f, 9999)]) });
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		var stored = await fixture.Context.MediaRatings.AsNoTracking().SingleAsync();
		using var _ = Assert.Multiple();
		await Assert.That(stored.Source).IsEqualTo(RatingProvider.Tmdb);
		await Assert.That(stored.NormalizedValue).IsEqualTo(9.0f);
		await Assert.That(stored.VoteCount).IsEqualTo(9999);
	}

	[Test]
	public async Task Update_WhenNewRatingsAreEmpty_RemovesExistingRatings()
	{
		var plexId = PlexId.From("plex-315", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var existing = await PersistAsync(
			CreateMovieDetails(mediaId, [plexId]) with
			{
				Rating = RatingSummary.From([new MediaRating(RatingProvider.Tmdb, 5.0f, 10)]),
			},
			mediaId
		);

		_testee.Update(existing, CreateMovieDetails(mediaId, [plexId]) with { Rating = RatingSummary.None });
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		await Assert.That(await fixture.Context.MediaRatings.AsNoTracking().AnyAsync()).IsFalse();
	}

	[Test]
	public async Task Update_WhenTypeMismatch_Throws()
	{
		var plexId = PlexId.From("plex-320", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var existing = await PersistAsync(CreateMovieDetails(mediaId, [plexId]), mediaId);
		var showDetails = CreateShowDetails(MediaId.From(Guid.NewGuid()), [TmdbId.From("321", MediaType.Show)]);

		await Assert.That(() => _testee.Update(existing, showDetails)).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task ToStored_WhenMovieEntity_ReturnsMovieWithMappedFields()
	{
		var plexId = PlexId.From("plex-400", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var entity = await PersistAsync(CreateMovieDetails(mediaId, [plexId]) with { Title = "Movie", Runtime = TimeSpan.FromMinutes(110) }, mediaId);

		var stored = _testee.ToStored(entity, new MediaIdentity(mediaId, [plexId]));

		using var _ = Assert.Multiple();
		await Assert.That(stored is Movie).IsTrue();
		await Assert.That(stored is Movie movie && movie.Identity.Id == mediaId).IsTrue();
		await Assert.That(stored is Movie { Title: "Movie" }).IsTrue();
		await Assert.That(stored is Movie m && m.Runtime == TimeSpan.FromMinutes(110)).IsTrue();
	}

	[Test]
	public async Task ToStored_WhenShowEntity_ReturnsShowWithMappedFields()
	{
		var plexId = PlexId.From("plex-410", MediaType.Show);
		var mediaId = await SeedMediaAsync([plexId]);
		var entity = await PersistAsync(CreateShowDetails(mediaId, [plexId]), mediaId);

		var stored = _testee.ToStored(entity, new MediaIdentity(mediaId, [plexId]));

		using var _ = Assert.Multiple();
		await Assert.That(stored is Show { Seasons: 3 }).IsTrue();
		await Assert.That(stored is Show { Episodes: 30 }).IsTrue();
		await Assert.That(stored is Show show && show.RuntimePerEpisode == TimeSpan.FromMinutes(45)).IsTrue();
	}

	[Test]
	public async Task ToStored_PassesThroughExternalsAndRatings()
	{
		var plexId = PlexId.From("plex-420", MediaType.Movie);
		var imdbId = ImdbId.From("tt420", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId, imdbId]);
		var entity = await PersistAsync(
			CreateMovieDetails(mediaId, [plexId, imdbId]) with
			{
				Rating = RatingSummary.From([new MediaRating(RatingProvider.Tmdb, 7.5f, 500)]),
			},
			mediaId
		);

		var stored = _testee.ToStored(entity, new MediaIdentity(mediaId, [plexId, imdbId]));

		using var _ = Assert.Multiple();
		await Assert.That(stored is Movie).IsTrue();
		await Assert.That(stored is Movie movie && movie.Identity.Externals.Contains(imdbId)).IsTrue();
		await Assert.That(stored is Movie { Rating.Sources.Count: 1 }).IsTrue();
		await Assert.That(stored is Movie m && m.Rating.Sources.Single().Source == RatingProvider.Tmdb).IsTrue();
	}

	private async Task<MediaMetadataRow> PersistAsync(IdentifiedMedia details, MediaId mediaId)
	{
		var entity = _testee.BuildFrom(details, mediaId.Value);
		fixture.Context.MediaMetadata.Add(entity);
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		return await fixture.Context.MediaMetadata.Include(m => m.Ratings).SingleAsync(m => m.MediaId == mediaId.Value);
	}

	private static Movie CreateMovieDetails(MediaId mediaId, IExternalMediaId[] externals) =>
		new()
		{
			Identity = new MediaIdentity(mediaId, [.. externals]),
			Title = "Title",
			Tagline = "Tagline",
			Summary = "Summary",
			PosterUrl = "https://example.com/poster.jpg",
			BackdropUrl = "https://example.com/backdrop.jpg",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Action"],
			Runtime = TimeSpan.FromMinutes(120),
		};

	private static Show CreateShowDetails(MediaId mediaId, IExternalMediaId[] externals) =>
		new()
		{
			Identity = new MediaIdentity(mediaId, [.. externals]),
			Title = "Show Title",
			Tagline = "Show Tagline",
			Summary = "Show Summary",
			PosterUrl = "https://example.com/show-poster.jpg",
			BackdropUrl = "https://example.com/show-backdrop.jpg",
			ReleaseDate = new DateOnly(2020, 1, 1),
			Genres = ["Drama"],
			Seasons = 3,
			Episodes = 30,
			RuntimePerEpisode = TimeSpan.FromMinutes(45),
		};
}
