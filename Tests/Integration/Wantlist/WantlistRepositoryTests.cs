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
	public async Task GetOwnedCountsAsync_ForTitlesAFeedAdded_CountsThemPerFeedAndIgnoresOtherOrigins()
	{
		var feedId = await SeedFeedAsync("owned-list");
		var origin = await fixture.SeedFeedOriginAsync(feedId);

		var first = await SeedMediaAsync([PlexId.From("plex-900", MediaType.Movie)]);
		var second = await SeedMediaAsync([PlexId.From("plex-901", MediaType.Movie)]);
		var byHand = await SeedMediaAsync([PlexId.From("plex-902", MediaType.Movie)]);

		await _testee.AddAsync([first, second], origin);
		await _testee.AddAsync([byHand], new WantlistItemProvenance(new ManualOrigin(ManualSource.Search)));

		var counts = await _testee.GetOwnedCountsAsync();

		await Assert.That(counts[feedId]).IsEqualTo(2);
		await Assert.That(counts).HasCount(1);
	}

	[Test]
	public async Task GetOwnedCountsAsync_ForAFeedThatOwnsNothing_LeavesItOutRatherThanReportingZero()
	{
		var feedId = await SeedFeedAsync("empty-list");

		var counts = await _testee.GetOwnedCountsAsync();

		await Assert.That(counts).DoesNotContainKey(feedId);
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
	public async Task AddAsync_BatchOfDistinctMedia_CreatesSeparateEntries()
	{
		var first = await SeedMediaAsync([PlexId.From("plex-200", MediaType.Movie)]);
		var second = await SeedMediaAsync([PlexId.From("plex-201", MediaType.Movie)]);

		await _testee.AddAsync([first, second], new WantlistItemProvenance(new SinkOrigin(SinkKind.Plex)));

		var entries = await fixture.Context.Wantlist.AsNoTracking().ToListAsync();
		await Assert.That(entries).Count().IsEqualTo(2);
	}

	[Test]
	public async Task AddAsync_ReturnsOnlyNewlyInsertedIds()
	{
		var alreadyWantlisted = await SeedWantlistItemAsync(PlexId.From("plex-300", MediaType.Movie), WantlistItemOrigin.Manual);
		var newMedia = await SeedMediaAsync([PlexId.From("plex-301", MediaType.Movie)]);
		var unknown = MediaId.From(Guid.NewGuid());

		var added = await _testee.AddAsync([alreadyWantlisted, newMedia, unknown], new WantlistItemProvenance(new ManualOrigin(ManualSource.Unknown)));

		await Assert.That(added).Count().IsEqualTo(1);
		await Assert.That(added).Contains(newMedia);
	}

	[Test]
	public async Task RemoveAsync_ReturnsOnlyActuallyRemovedIds()
	{
		var wantlisted = await SeedWantlistItemAsync(PlexId.From("plex-302", MediaType.Movie), WantlistItemOrigin.Manual);
		var unknown = MediaId.From(Guid.NewGuid());

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
		var unknown = MediaId.From(Guid.NewGuid());

		await _testee.RemoveAsync([unknown]);

		var count = await fixture.Context.Wantlist.CountAsync();
		await Assert.That(count).IsEqualTo(1);
	}

	[Test]
	public async Task GetIdsAsync_ReturnsAllEntries()
	{
		var mediaId = await SeedWantlistItemAsync(PlexId.From("plex-105", MediaType.Movie), WantlistItemOrigin.Manual);

		var ids = await _testee.GetIdsAsync();

		await Assert.That(ids).Count().IsEqualTo(1);
		await Assert.That(ids[0]).IsEqualTo(mediaId);
	}

	[Test]
	public async Task GetEntriesAsync_ReturnsEntries_WithProvenance()
	{
		var mediaId = await SeedWantlistItemAsync(PlexId.From("plex-106", MediaType.Movie), WantlistItemOrigin.Manual);

		var entries = await _testee.GetEntriesAsync();

		await Assert.That(entries).Count().IsEqualTo(1);
		await Assert.That(entries[0].MediaId).IsEqualTo(mediaId);
		await Assert.That(entries[0].Provenance.Value).IsTypeOf<ManualOrigin>();
	}

	[Test]
	public async Task GetEntriesAsync_SinkEntry_CarriesStoredSink()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-107", MediaType.Movie)]);
		await _testee.AddAsync([mediaId], new WantlistItemProvenance(new SinkOrigin(SinkKind.Plex)));

		var entries = await _testee.GetEntriesAsync();

		await Assert.That(entries[0].Provenance).IsOfUnionType<WantlistItemProvenance, SinkOrigin>();
		var remote = (SinkOrigin)entries[0].Provenance.Value!;
		await Assert.That(remote.Provider).IsEqualTo(SinkKind.Plex);
	}

	[Test]
	public async Task GetEntriesAsync_ManualEntry_CarriesStoredSource()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-110", MediaType.Movie)]);
		await _testee.AddAsync([mediaId], new WantlistItemProvenance(new ManualOrigin(ManualSource.Browse)));

		var entries = await _testee.GetEntriesAsync();

		await Assert.That(entries[0].Provenance).IsOfUnionType<WantlistItemProvenance, ManualOrigin>();
		var manual = (ManualOrigin)entries[0].Provenance.Value!;
		await Assert.That(manual.Source).IsEqualTo(ManualSource.Browse);
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
