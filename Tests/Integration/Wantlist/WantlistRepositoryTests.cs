using DB.Data.Feed;
using DB.Data.Media;
using DB.Data.Wantlist;
using Domain.Feed;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using Harness;
using Integration.Tests.Feeds;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Wantlist;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class WantlistRepositoryTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private WantlistRepository _testee = null!;

	[Before(Test)]
	public void Setup() => _testee = new WantlistRepository(fixture.ContextFactory);

	[Test]
	public async Task CountByFeedAsync_ForTitlesAFeedAdded_CountsThemPerFeedAndIgnoresOtherOrigins()
	{
		var feedId = await SeedFeedAsync("owned-list");
		var origin = await fixture.SeedFeedOriginAsync(feedId);

		var first = await SeedMediaAsync([PlexId.From("plex-900", MediaType.Movie)]);
		var second = await SeedMediaAsync([PlexId.From("plex-901", MediaType.Movie)]);
		var byHand = await SeedMediaAsync([PlexId.From("plex-902", MediaType.Movie)]);

		await _testee.AddAsync([first, second], origin);
		await _testee.AddAsync([byHand], new WantlistItemProvenance(new ManualOrigin(ManualSource.Search)));

		var counts = await _testee.CountByFeedAsync();

		await Assert.That(counts[feedId]).IsEqualTo(2);
		await Assert.That(counts).Count().IsEqualTo(1);
	}

	[Test]
	public async Task CountByFeedAsync_ForAFeedThatAddedNothing_LeavesItOutRatherThanReportingZero()
	{
		var feedId = await SeedFeedAsync("empty-list");

		var counts = await _testee.CountByFeedAsync();

		await Assert.That(counts).DoesNotContainKey(feedId);
	}

	[Test]
	public async Task OrphanAsync_ForTheTitlesAFeedAdded_KeepsThemUnderTheFeedsNameWithoutItsRun()
	{
		var feedId = await SeedFeedAsync("my-feed");
		var fromFeed = await SeedMediaAsync([PlexId.From("plex-950", MediaType.Movie)]);
		var byHand = await SeedMediaAsync([PlexId.From("plex-951", MediaType.Movie)]);
		await _testee.AddAsync([fromFeed], await fixture.SeedFeedOriginAsync(feedId));
		await _testee.AddAsync([byHand], new WantlistItemProvenance(new ManualOrigin(ManualSource.Search)));
		await _testee.OrphanAsync(await fixture.FeedAsync(feedId));

		var rows = await fixture
			.Context.Wantlist.AsNoTracking()
			.Select(w => new
			{
				w.Media.Id,
				w.Origin,
				w.SourceFeedNameSnapshot,
				w.SourceRunId,
			})
			.ToListAsync();
		await Assert
			.That(rows.Single(row => row.Id == fromFeed.Value))
			.IsEqualTo(
				new
				{
					Id = fromFeed.Value,
					Origin = WantlistItemOrigin.Orphaned,
					SourceFeedNameSnapshot = (string?)"my-feed",
					SourceRunId = (Guid?)null,
				}
			);
		await Assert.That(rows.Single(row => row.Id == byHand.Value).Origin).IsEqualTo(WantlistItemOrigin.Manual);
	}

	private async Task<FeedId> SeedFeedAsync(string identifier)
	{
		var entity = new FeedRow
		{
			Name = identifier,
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = new FeedStrategy(new Append()),
			Identifier = identifier,
		};

		fixture.Context.Feeds.Add(entity);
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return FeedId.From(entity.Id);
	}

	[Test]
	public async Task AddAsync_BatchOfDistinctMedia_CreatesOneRowEach()
	{
		var first = await SeedMediaAsync([PlexId.From("plex-200", MediaType.Movie)]);
		var second = await SeedMediaAsync([PlexId.From("plex-201", MediaType.Movie)]);

		await _testee.AddAsync([first, second], new WantlistItemProvenance(new SinkOrigin(SinkKind.Plex)));

		var rows = await fixture.Context.Wantlist.AsNoTracking().ToListAsync();
		await Assert.That(rows).Count().IsEqualTo(2);
	}

	[Test]
	public async Task AddAsync_ReturnsOnlyNewlyInsertedIds()
	{
		var alreadyWantlisted = await SeedWantlistItemAsync(PlexId.From("plex-300", MediaType.Movie), WantlistItemOrigin.Manual);
		var newMedia = await SeedMediaAsync([PlexId.From("plex-301", MediaType.Movie)]);
		var unknown = MediaId.New();

		var added = await _testee.AddAsync([alreadyWantlisted, newMedia, unknown], new WantlistItemProvenance(new ManualOrigin(ManualSource.Unknown)));

		await Assert.That(added).Count().IsEqualTo(1);
		await Assert.That(added).Contains(newMedia);
	}

	[Test]
	public async Task RemoveAsync_ReturnsOnlyActuallyRemovedIds()
	{
		var wantlisted = await SeedWantlistItemAsync(PlexId.From("plex-302", MediaType.Movie), WantlistItemOrigin.Manual);
		var unknown = MediaId.New();

		var removed = await _testee.RemoveAsync([wantlisted, unknown]);

		await Assert.That(removed).Count().IsEqualTo(1);
		await Assert.That(removed).Contains(wantlisted);
	}

	[Test]
	public async Task AddAsync_EmptyCollection_DoesNothing()
	{
		await _testee.AddAsync([], new WantlistItemProvenance(new ManualOrigin(ManualSource.Unknown)));

		var count = await fixture.Context.Wantlist.CountAsync();
		await Assert.That(count).IsEqualTo(0);
	}

	[Test]
	public async Task RemoveAsync_EmptyCollection_DoesNothing()
	{
		await SeedWantlistItemAsync(PlexId.From("plex-108", MediaType.Movie), WantlistItemOrigin.Manual);

		await _testee.RemoveAsync([]);

		var count = await fixture.Context.Wantlist.CountAsync();
		await Assert.That(count).IsEqualTo(1);
	}

	[Test]
	public async Task RemoveAsync_UnknownId_IsNoOp()
	{
		await SeedWantlistItemAsync(PlexId.From("plex-109", MediaType.Movie), WantlistItemOrigin.Manual);
		var unknown = MediaId.New();

		await _testee.RemoveAsync([unknown]);

		var count = await fixture.Context.Wantlist.CountAsync();
		await Assert.That(count).IsEqualTo(1);
	}

	[Test]
	public async Task RemoveAsync_ByFeed_RemovesOnlyTheTitlesThatFeedAdded()
	{
		var feedId = await SeedFeedAsync("mirror-list");
		var otherFeedId = await SeedFeedAsync("other-list");
		var fromFeed = await SeedMediaAsync([PlexId.From("plex-400", MediaType.Movie)]);
		var fromOtherFeed = await SeedMediaAsync([PlexId.From("plex-401", MediaType.Movie)]);
		var byHand = await SeedMediaAsync([PlexId.From("plex-402", MediaType.Movie)]);
		await _testee.AddAsync([fromFeed], await fixture.SeedFeedOriginAsync(feedId));
		await _testee.AddAsync([fromOtherFeed], await fixture.SeedFeedOriginAsync(otherFeedId));
		await _testee.AddAsync([byHand], new WantlistItemProvenance(new ManualOrigin(ManualSource.Search)));

		var removed = await _testee.RemoveAsync([fromFeed, fromOtherFeed, byHand], addedBy: feedId);

		await Assert.That(removed).IsEquivalentTo([fromFeed]);
		await Assert.That((await _testee.GetAllAsync()).Select(item => item.MediaId)).IsEquivalentTo([fromOtherFeed, byHand]);
	}

	[Test]
	public async Task GetAllAsync_ReturnsTheItems_WithTheirProvenance()
	{
		var mediaId = await SeedWantlistItemAsync(PlexId.From("plex-106", MediaType.Movie), WantlistItemOrigin.Manual);

		var items = await _testee.GetAllAsync();

		await Assert.That(items).Count().IsEqualTo(1);
		await Assert.That(items[0].MediaId).IsEqualTo(mediaId);
		await Assert.That(items[0].Provenance).IsOfUnionType<WantlistItemProvenance, ManualOrigin>();
	}

	[Test]
	public async Task GetAllAsync_ForASinkItem_CarriesTheStoredSink()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-107", MediaType.Movie)]);
		await _testee.AddAsync([mediaId], new WantlistItemProvenance(new SinkOrigin(SinkKind.Plex)));

		var items = await _testee.GetAllAsync();

		await Assert
			.That(items[0].Provenance)
			.IsOfUnionType<WantlistItemProvenance, SinkOrigin>()
			.And.Multiple(async sink => await Assert.That(sink.Provider).IsEqualTo(SinkKind.Plex));
	}

	[Test]
	public async Task GetAllAsync_ForAManualItem_CarriesTheStoredSource()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-110", MediaType.Movie)]);
		await _testee.AddAsync([mediaId], new WantlistItemProvenance(new ManualOrigin(ManualSource.Browse)));

		var items = await _testee.GetAllAsync();

		await Assert
			.That(items[0].Provenance)
			.IsOfUnionType<WantlistItemProvenance, ManualOrigin>()
			.And.Multiple(async manual => await Assert.That(manual.Source).IsEqualTo(ManualSource.Browse));
	}

	private async Task<MediaId> SeedMediaAsync(IEnumerable<IExternalMediaId> ids)
	{
		var media = new MediaRow();
		await fixture.Context.ExternalMediaIds.AddRangeAsync(ids.Select(id => id.ToExternalIdEntity(media)));
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return MediaId.From(media.Id);
	}

	private async Task<MediaId> SeedWantlistItemAsync(IExternalMediaId id, WantlistItemOrigin origin)
	{
		var media = new MediaRow();
		await fixture.Context.ExternalMediaIds.AddAsync(id.ToExternalIdEntity(media));
		fixture.Context.Wantlist.Add(new WantlistRow { Media = media, Origin = origin });
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return MediaId.From(media.Id);
	}
}
