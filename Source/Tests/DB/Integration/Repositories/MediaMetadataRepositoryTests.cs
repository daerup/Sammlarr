using Contracts;
using Contracts.Caching;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Settings;
using DB.Data.Blacklist;
using DB.Data.Config;
using DB.Data.MediaGroup;
using DB.Data.Metadata;
using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration.Repositories;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class MediaMetadataRepositoryTests(SQLiteFixture fixture) : IAsyncDisposable
{
	private MediaMetadataRepository _testee = null!;
	private Mock<IDateTimeProvider> _dateTimeProvider = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup()
	{
		_dateTimeProvider = IDateTimeProvider.Mock();
		_dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

		_testee = new MediaMetadataRepository(
			fixture.ContextFactory,
			new MediaIdGroupRepository(fixture.ContextFactory),
			new MediaMetadataFactory(_dateTimeProvider.Object),
			new ProviderConfigRepository(fixture.ContextFactory, ITypedMemoryCache<string, ProviderConfig>.Mock())
		);
	}

	[Test]
	public async Task GetByMediaIdAsync_WhenAnchorMatches_ReturnsCachedDetails()
	{
		var mediaId = TmdbId.From("120", MediaType.Movie);
		var imdbId = ImdbId.From("tt120", MediaType.Movie);
		await _testee.UpsertManyAsync([new MediaMetadataUpsert(mediaId, CreateMovieDetails(mediaId, [imdbId]))]);
		fixture.Context.ChangeTracker.Clear();

		var cached = await _testee.GetByMediaIdAsync(mediaId);

		await Assert.That(cached).IsNotNull();
		await Assert.That(cached!.Details.SourceId).IsEqualTo(mediaId);
		await Assert.That(cached.Details.AdditionalIds).Contains(imdbId);
	}

	[Test]
	public async Task GetByMediaIdAsync_WhenLookedUpByLinkedId_ReturnsDetailsWithThatIdAsAnchor()
	{
		var mediaId = TmdbId.From("130", MediaType.Movie);
		var imdbId = ImdbId.From("tt130", MediaType.Movie);
		await _testee.UpsertManyAsync([new MediaMetadataUpsert(mediaId, CreateMovieDetails(mediaId, [imdbId]))]);
		fixture.Context.ChangeTracker.Clear();

		var cached = await _testee.GetByMediaIdAsync(imdbId);

		await Assert.That(cached).IsNotNull();
		await Assert.That(cached!.Details.SourceId).IsEqualTo(imdbId);
		await Assert.That(cached.Details.AdditionalIds).Contains(mediaId);
	}

	[Test]
	public async Task GetByMediaIdAsync_WhenNoEntryExists_ReturnsNull()
	{
		var cached = await _testee.GetByMediaIdAsync(TmdbId.From("999", MediaType.Movie));

		await Assert.That(cached).IsNull();
	}

	[Test]
	public async Task UpsertAsync_AdvancesLastFetchedAtEvenWhenPayloadIsIdentical()
	{
		var mediaId = TmdbId.From("700", MediaType.Movie);
		var firstFetch = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var secondFetch = firstFetch.AddHours(2);

		_dateTimeProvider.Now.Returns(firstFetch);
		await _testee.UpsertManyAsync([new MediaMetadataUpsert(mediaId, CreateMovieDetails(mediaId, []))]);
		fixture.Context.ChangeTracker.Clear();

		_dateTimeProvider.Now.Returns(secondFetch);
		await _testee.UpsertManyAsync([new MediaMetadataUpsert(mediaId, CreateMovieDetails(mediaId, []))]);
		fixture.Context.ChangeTracker.Clear();

		var cached = await _testee.GetByMediaIdAsync(mediaId);
		await Assert.That(cached!.FetchedAt).IsEqualTo(secondFetch);
	}

	[Test]
	public async Task PurgeAllAsync_WhenGroupIsBlacklisted_KeepsGroupAndItsEntries()
	{
		var blacklistedId = PlexId.From("2000", MediaType.Movie);
		var blacklistedImdb = ImdbId.From("tt2000", MediaType.Movie);
		var purgedId = TmdbId.From("2001", MediaType.Movie);

		var blacklistedGroup = new MediaIdentifierGroupEntity();
		var purgedGroup = new MediaIdentifierGroupEntity();
		fixture.Context.MediaIdEntries.AddRange(
			new MediaIdentifierGroupEntry { Identifier = MediaIdentifier.From(blacklistedId), MediaIdGroupEntity = blacklistedGroup },
			new MediaIdentifierGroupEntry { Identifier = MediaIdentifier.From(blacklistedImdb), MediaIdGroupEntity = blacklistedGroup },
			new MediaIdentifierGroupEntry { Identifier = MediaIdentifier.From(purgedId), MediaIdGroupEntity = purgedGroup }
		);
		var blacklistedMetadata = BuildMetadata(blacklistedGroup, "Blacklisted");
		var purgedMetadata = BuildMetadata(purgedGroup, "Purged");
		fixture.Context.MediaMetadata.AddRange(blacklistedMetadata, purgedMetadata);
		fixture.Context.MediaRatings.AddRange(
			new MediaRatingEntity { Source = RatingSource.Tmdb, NormalizedValue = 8.0f, VoteCount = 1234, Metadata = blacklistedMetadata },
			new MediaRatingEntity { Source = RatingSource.Tmdb, NormalizedValue = 7.0f, VoteCount = 567, Metadata = purgedMetadata }
		);
		fixture.Context.BlacklistEntries.Add(new BlacklistEntryEntity { Media = blacklistedGroup });
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		await _testee.PurgeAllAsync();

		using var _ = Assert.Multiple();
		await Assert.That(await fixture.Context.MediaMetadata.AnyAsync()).IsFalse();
		await Assert.That(await fixture.Context.MediaRatings.AnyAsync()).IsFalse();
		await Assert.That(await fixture.Context.BlacklistEntries.CountAsync()).IsEqualTo(1);
		await Assert.That(await fixture.Context.MediaIdGroups.CountAsync()).IsEqualTo(1);
		await Assert.That(await fixture.Context.MediaIdGroups.Select(g => g.Id).SingleAsync()).IsEqualTo(blacklistedGroup.Id);
		await Assert.That(fixture.Context.MediaIdEntries).IsSingleElement();
		await Assert.That(fixture.Context.MediaIdEntries.Single().Identifier).IsEqualTo(MediaIdentifier.From(blacklistedId));
	}

	private static MovieMetadataEntity BuildMetadata(MediaIdentifierGroupEntity group, string title) =>
		new()
		{
			MediaIdGroup = group,
			Title = title,
			Tagline = "Tagline",
			Summary = "Summary",
			PosterUrl = "https://example.com/poster.jpg",
			BackdropUrl = "https://example.com/backdrop.jpg",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Action"],
			Runtime = TimeSpan.FromMinutes(120),
		};

	[Test]
	public async Task UpsertAsync_WhenEntryExists_PreservesCreatedAt()
	{
		var mediaId = TmdbId.From("500", MediaType.Movie);
		await _testee.UpsertManyAsync([new MediaMetadataUpsert(mediaId, CreateMovieDetails(mediaId, []))]);
		fixture.Context.ChangeTracker.Clear();

		var marker = new DateTimeOffset(2020, 6, 15, 0, 0, 0, TimeSpan.Zero);
		await fixture.Context.MediaMetadata.ExecuteUpdateAsync(s => s.SetProperty(m => m.CreatedAt, marker));

		await _testee.UpsertManyAsync([new MediaMetadataUpsert(mediaId, CreateMovieDetails(mediaId, []) with { Title = "Updated" })]);
		fixture.Context.ChangeTracker.Clear();

		var stored = await fixture.Context.MediaMetadata.AsNoTracking().SingleAsync();
		using var _ = Assert.Multiple();
		await Assert.That(stored.Title).IsEqualTo("Updated");
		await Assert.That(stored.CreatedAt).IsEqualTo(marker);
	}

	private static MovieDetailResult CreateMovieDetails(IMediaId sourceId, IMediaId[] additionalIds) =>
		new()
		{
			SourceId = sourceId,
			AdditionalIds = additionalIds,
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
