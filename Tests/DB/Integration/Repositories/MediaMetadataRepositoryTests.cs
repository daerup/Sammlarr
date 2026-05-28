using Contracts;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using DB.Data.Media;
using DB.Data.Metadata;
using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration.Repositories;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class MediaMetadataRepositoryTests(SQLiteFixture fixture) : IAsyncDisposable
{
	private MediaMetadataRepository _testee = null!;
	private MediaRepository _mediaRepo = null!;
	private Mock<IDateTimeProvider> _dateTimeProvider = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup()
	{
		_dateTimeProvider = IDateTimeProvider.Mock();
		_dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

		_mediaRepo = new MediaRepository(fixture.ContextFactory);
		_testee = new MediaMetadataRepository(fixture.ContextFactory, new MediaMetadataFactory(_dateTimeProvider.Object));
	}

	private async Task<MediaId> SeedMediaAsync(IEnumerable<IExternalMediaId> ids)
	{
		var media = new MediaEntity();
		await fixture.Context.ExternalMediaIds.AddRangeAsync(ids.Select(id => id.ToExternalIdEntity(media)));
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return MediaId.From(media.Id);
	}

	[Test]
	public async Task UpsertAsync_AdvancesLastFetchedAtEvenWhenPayloadIsIdentical()
	{
		var plexId = PlexId.From("plex-700", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var firstFetch = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var secondFetch = firstFetch.AddHours(2);

		_dateTimeProvider.Now.Returns(firstFetch);
		await _testee.UpsertManyAsync([new MediaMetadataUpsert(mediaId, CreateMovieDetails(mediaId, [plexId]))]);
		fixture.Context.ChangeTracker.Clear();

		_dateTimeProvider.Now.Returns(secondFetch);
		await _testee.UpsertManyAsync([new MediaMetadataUpsert(mediaId, CreateMovieDetails(mediaId, [plexId]))]);
		fixture.Context.ChangeTracker.Clear();

		var sessionFactory = new MediaQuerySessionFactory(fixture.ContextFactory);
		await using var session = await sessionFactory.CreateAsync();
		var query = _mediaRepo.QueryMediaIds(session, [mediaId]);
		var row = (await _testee.QueryMediaMetadataAsync(session, query)).Single();
		await Assert.That(row.FetchedAt).IsEqualTo(secondFetch);
	}

	[Test]
	public async Task UpsertAsync_WhenEntryExists_PreservesCreatedAt()
	{
		var plexId = PlexId.From("plex-500", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		await _testee.UpsertManyAsync([new MediaMetadataUpsert(mediaId, CreateMovieDetails(mediaId, [plexId]))]);
		fixture.Context.ChangeTracker.Clear();

		var marker = new DateTimeOffset(2020, 6, 15, 0, 0, 0, TimeSpan.Zero);
		await fixture.Context.MediaMetadata.ExecuteUpdateAsync(s => s.SetProperty(m => m.CreatedAt, marker));

		await _testee.UpsertManyAsync([new MediaMetadataUpsert(mediaId, CreateMovieDetails(mediaId, [plexId]) with { Title = "Updated" })]);
		fixture.Context.ChangeTracker.Clear();

		var stored = await fixture.Context.MediaMetadata.AsNoTracking().SingleAsync();
		using var _ = Assert.Multiple();
		await Assert.That(stored.Title).IsEqualTo("Updated");
		await Assert.That(stored.CreatedAt).IsEqualTo(marker);
	}

	[Test]
	public async Task QueryMediaMetadata_WhenMetadataExists_YieldsCachedDetailWithFetchedAt()
	{
		var plexId = PlexId.From("plex-810", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		await _testee.UpsertManyAsync([new MediaMetadataUpsert(mediaId, CreateMovieDetails(mediaId, [plexId]))]);
		fixture.Context.ChangeTracker.Clear();

		var sessionFactory = new MediaQuerySessionFactory(fixture.ContextFactory);
		await using var session = await sessionFactory.CreateAsync();
		var query = _mediaRepo.QueryMediaIds(session, [mediaId]);
		var row = (await _testee.QueryMediaMetadataAsync(session, query)).Single();

		await Assert.That(row.Detail.Identity.Id).IsEqualTo(mediaId);
		await Assert.That(row.Detail).IsTypeOf<MovieDetail>();
		await Assert.That(row.Detail.Identity.Externals).Contains((IExternalMediaId)plexId);
		await Assert.That(row.FetchedAt).IsNotNull();
	}

	[Test]
	public async Task QueryMediaMetadata_WhenMetadataMissing_YieldsUnresolvedMediaWithoutFetchedAt()
	{
		var plexId = PlexId.From("plex-811", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		fixture.Context.ChangeTracker.Clear();

		var sessionFactory = new MediaQuerySessionFactory(fixture.ContextFactory);
		await using var session = await sessionFactory.CreateAsync();
		var query = _mediaRepo.QueryMediaIds(session, [mediaId]);
		var row = (await _testee.QueryMediaMetadataAsync(session, query)).Single();

		await Assert.That(row.Detail).IsTypeOf<UnresolvedMedia>();
		await Assert.That(row.Detail.Identity.Id).IsEqualTo(mediaId);
		await Assert.That(row.FetchedAt).IsNull();
	}

	[Test]
	public async Task QueryMediaMetadata_MixedHitAndMiss_YieldsBothInOneRoundTrip()
	{
		var firstPlex = PlexId.From("plex-812", MediaType.Movie);
		var secondPlex = PlexId.From("plex-813", MediaType.Movie);
		var firstId = await SeedMediaAsync([firstPlex]);
		var secondId = await SeedMediaAsync([secondPlex]);
		await _testee.UpsertManyAsync([new MediaMetadataUpsert(firstId, CreateMovieDetails(firstId, [firstPlex]))]);
		fixture.Context.ChangeTracker.Clear();

		var sessionFactory = new MediaQuerySessionFactory(fixture.ContextFactory);
		await using var session = await sessionFactory.CreateAsync();
		var query = _mediaRepo.QueryMediaIds(session, [firstId, secondId]);
		var rows = await _testee.QueryMediaMetadataAsync(session, query);

		await Assert.That(rows.Count).IsEqualTo(2);
		await Assert.That(rows.Single(r => r.Detail.Identity.Id == firstId).Detail).IsTypeOf<MovieDetail>();
		await Assert.That(rows.Single(r => r.Detail.Identity.Id == secondId).Detail).IsTypeOf<UnresolvedMedia>();
	}

	private static MovieDetail CreateMovieDetails(MediaId mediaId, IExternalMediaId[] externals) =>
		new()
		{
			Identity = new MediaIdentity(mediaId, externals),
			Title = "Title",
			Tagline = "Tagline",
			Summary = "Summary",
			PosterUrl = "https://example.com/poster.jpg",
			BackdropUrl = "https://example.com/backdrop.jpg",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Action"],
			Runtime = TimeSpan.FromMinutes(120),
		};
}
