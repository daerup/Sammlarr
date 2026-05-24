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

	[Test]
	public async Task GetByMediaIdAsync_WhenEntryExists_ReturnsCachedDetails()
	{
		var plexId = PlexId.From("plex-120", MediaType.Movie);
		var imdbId = ImdbId.From("tt120", MediaType.Movie);
		var mediaId = (await _mediaRepo.UpsertAsync([plexId, imdbId])).Id;
		await _testee.UpsertManyAsync([new MediaMetadataUpsert(mediaId, CreateMovieDetails(mediaId, [plexId, imdbId]))]);
		fixture.Context.ChangeTracker.Clear();

		var cached = await _testee.GetByMediaIdAsync(mediaId);

		await Assert.That(cached).IsNotNull();
		await Assert.That(cached!.Details.Id).IsEqualTo(mediaId);
		await Assert.That(cached.Details.Externals).Contains((IExternalMediaId)imdbId);
		await Assert.That(cached.Details.Externals).Contains((IExternalMediaId)plexId);
	}

	[Test]
	public async Task GetByMediaIdAsync_WhenNoEntryExists_ReturnsNull()
	{
		var cached = await _testee.GetByMediaIdAsync(MediaId.From(Guid.NewGuid()));

		await Assert.That(cached).IsNull();
	}

	[Test]
	public async Task UpsertAsync_AdvancesLastFetchedAtEvenWhenPayloadIsIdentical()
	{
		var plexId = PlexId.From("plex-700", MediaType.Movie);
		var mediaId = (await _mediaRepo.UpsertAsync([plexId])).Id;
		var firstFetch = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var secondFetch = firstFetch.AddHours(2);

		_dateTimeProvider.Now.Returns(firstFetch);
		await _testee.UpsertManyAsync([new MediaMetadataUpsert(mediaId, CreateMovieDetails(mediaId, [plexId]))]);
		fixture.Context.ChangeTracker.Clear();

		_dateTimeProvider.Now.Returns(secondFetch);
		await _testee.UpsertManyAsync([new MediaMetadataUpsert(mediaId, CreateMovieDetails(mediaId, [plexId]))]);
		fixture.Context.ChangeTracker.Clear();

		var cached = await _testee.GetByMediaIdAsync(mediaId);
		await Assert.That(cached!.FetchedAt).IsEqualTo(secondFetch);
	}

	[Test]
	public async Task UpsertAsync_WhenEntryExists_PreservesCreatedAt()
	{
		var plexId = PlexId.From("plex-500", MediaType.Movie);
		var mediaId = (await _mediaRepo.UpsertAsync([plexId])).Id;
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

	private static MovieDetailResult CreateMovieDetails(MediaId mediaId, IExternalMediaId[] externals) =>
		new()
		{
			Id = mediaId,
			Externals = externals.ToList(),
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
