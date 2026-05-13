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
using DB.Data.MediaGroup;
using DB.Data.Metadata;
using Microsoft.EntityFrameworkCore;

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
					TrendingProvider = MediaRecommendationProvider.Tmdb,
					SearchProvider = MediaSearchProvider.Plex,
					RatingProvider = RatingProvider.Tmdb,
				}
			);
		_testee = new MediaCachePurger(fixture.ContextFactory, configRepository.Object);
	}

	[Test]
	public async Task PurgeAsync_WhenGroupIsBlacklisted_KeepsGroupAndOnlyItsAnchorEntry()
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

		await _testee.PurgeAsync();

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
}
