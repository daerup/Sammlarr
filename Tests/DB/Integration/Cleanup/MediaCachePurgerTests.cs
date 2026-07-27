using Contracts.Auth;
using Contracts.Caching;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;
using DB.Data.Blacklist;
using DB.Data.Cleanup;
using DB.Data.Config;
using DB.Data.Media;
using DB.Data.Metadata;
using DB.Data.Watchlist;
using DB.Data.Watchlist.Outbox;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using Domain.Settings;
using Domain.Watchlist.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DB.Tests.Integration.Cleanup;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class MediaCachePurgerTests(SQLiteFixture fixture) : IAsyncDisposable
{
	private IMediaCachePurger _testee = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup()
	{
		var configRepository = IProviderConfigRepository.Mock();
		configRepository
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					WatchlistProvider = WatchlistProvider.Plex,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Plex,
					RatingProvider = RatingProvider.Tmdb,
					AuthProvider = AuthProvider.Plex,
				}
			);
		_testee = new MediaCachePurger(fixture.ContextFactory, configRepository.Object, NullLogger<MediaCachePurger>.Instance);
	}

	[Test]
	public async Task PurgeAsync_WhenMediaIsBlacklisted_KeepsMediaAndOnlyItsAnchorExternalId()
	{
		var blacklistedId = PlexId.From("2000", MediaType.Movie);
		var blacklistedImdb = ImdbId.From("tt2000", MediaType.Movie);
		var purgedId = TmdbId.From("2001", MediaType.Movie);

		var blacklistedMedia = new MediaRow();
		var purgedMedia = new MediaRow();
		fixture.Context.ExternalMediaIds.AddRange(
			blacklistedId.ToExternalIdEntity(blacklistedMedia),
			blacklistedImdb.ToExternalIdEntity(blacklistedMedia),
			purgedId.ToExternalIdEntity(purgedMedia)
		);
		var blacklistedMetadata = BuildMetadata(blacklistedMedia, "Blacklisted");
		var purgedMetadata = BuildMetadata(purgedMedia, "Purged");
		fixture.Context.MediaMetadata.AddRange(blacklistedMetadata, purgedMetadata);
		fixture.Context.MediaRatings.AddRange(
			new MediaRatingRow
			{
				Source = RatingProvider.Tmdb,
				NormalizedValue = 8.0f,
				VoteCount = 1234,
				Metadata = blacklistedMetadata,
			},
			new MediaRatingRow
			{
				Source = RatingProvider.Tmdb,
				NormalizedValue = 7.0f,
				VoteCount = 567,
				Metadata = purgedMetadata,
			}
		);
		fixture.Context.Blacklist.Add(new BlacklistRow { Media = blacklistedMedia });
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		await _testee.PurgeAsync();

		using var _ = Assert.Multiple();
		await Assert.That(await fixture.Context.MediaMetadata.AnyAsync()).IsFalse();
		await Assert.That(await fixture.Context.MediaRatings.AnyAsync()).IsFalse();
		await Assert.That(await fixture.Context.Blacklist.CountAsync()).IsEqualTo(1);
		await Assert.That(await fixture.Context.Media.CountAsync()).IsEqualTo(1);
		await Assert.That(await fixture.Context.Media.Select(m => m.Id).SingleAsync()).IsEqualTo(blacklistedMedia.Id);
		await Assert.That(fixture.Context.ExternalMediaIds).IsSingleElement();
		await Assert.That(fixture.Context.ExternalMediaIds.Single().ToExternalMediaId()).IsEqualTo<IExternalMediaId>(blacklistedId);
	}

	[Test]
	public async Task PurgeAsync_WhenMediaIsWatchlisted_KeepsMediaAndOnlyItsAnchorExternalId()
	{
		var watchlistedId = PlexId.From("3000", MediaType.Movie);
		var watchlistedImdb = ImdbId.From("tt3000", MediaType.Movie);

		var watchlistedMedia = new MediaRow();
		fixture.Context.ExternalMediaIds.AddRange(watchlistedId.ToExternalIdEntity(watchlistedMedia), watchlistedImdb.ToExternalIdEntity(watchlistedMedia));
		var metadata = BuildMetadata(watchlistedMedia, "Watchlisted");
		fixture.Context.MediaMetadata.Add(metadata);
		fixture.Context.Watchlist.Add(new WatchlistRow { Media = watchlistedMedia, Origin = WatchlistEntryOrigin.Manual });
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		await _testee.PurgeAsync();

		using var _ = Assert.Multiple();
		await Assert.That(await fixture.Context.Watchlist.CountAsync()).IsEqualTo(1);
		await Assert.That(await fixture.Context.Media.CountAsync()).IsEqualTo(1);
		await Assert.That(await fixture.Context.Media.Select(m => m.Id).SingleAsync()).IsEqualTo(watchlistedMedia.Id);
		await Assert.That(fixture.Context.ExternalMediaIds.Single().ToExternalMediaId()).IsEqualTo<IExternalMediaId>(watchlistedId);
	}

	[Test]
	public async Task PurgeAsync_WhenMediaIsInOutbox_KeepsMediaAndOnlyItsAnchorExternalId()
	{
		var queuedId = PlexId.From("4000", MediaType.Movie);
		var queuedImdb = ImdbId.From("tt4000", MediaType.Movie);

		var queuedMedia = new MediaRow();
		fixture.Context.ExternalMediaIds.AddRange(queuedId.ToExternalIdEntity(queuedMedia), queuedImdb.ToExternalIdEntity(queuedMedia));
		fixture.Context.WatchlistOutbox.Add(new WatchlistOutboxRow { Media = queuedMedia, Kind = WatchlistOperationKind.Add });
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();

		await _testee.PurgeAsync();

		using var _ = Assert.Multiple();
		await Assert.That(await fixture.Context.WatchlistOutbox.CountAsync()).IsEqualTo(1);
		await Assert.That(await fixture.Context.Media.CountAsync()).IsEqualTo(1);
		await Assert.That(await fixture.Context.Media.Select(m => m.Id).SingleAsync()).IsEqualTo(queuedMedia.Id);
		await Assert.That(fixture.Context.ExternalMediaIds.Single().ToExternalMediaId()).IsEqualTo<IExternalMediaId>(queuedId);
	}

	private static MovieMetadataRow BuildMetadata(MediaRow media, string title) =>
		new()
		{
			Media = media,
			Title = title,
			Tagline = "Tagline",
			Summary = "Summary",
			PosterUrl = "https://example.com/poster.jpg",
			BackdropUrl = "https://example.com/backdrop.jpg",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Action"],
			Runtime = TimeSpan.FromMinutes(120),
		};
}
