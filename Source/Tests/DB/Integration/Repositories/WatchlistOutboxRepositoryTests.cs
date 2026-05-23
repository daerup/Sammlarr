using Contracts.Media;
using Contracts.Watchlist.Outbox;
using DB.Data.Media;
using DB.Data.Watchlist.Outbox;
using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration.Repositories;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class WatchlistOutboxRepositoryTests(SQLiteFixture fixture) : IAsyncDisposable
{
	private WatchlistOutboxRepository _testee = null!;
	private MediaRepository _mediaRepo = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup()
	{
		_mediaRepo = new MediaRepository(fixture.ContextFactory);
		_testee = new WatchlistOutboxRepository(fixture.ContextFactory, _mediaRepo);
	}

	[Test]
	public async Task EnqueueAsync_NewId_InsertsRow()
	{
		var id = PlexId.From("plex-200", MediaType.Movie);

		await _testee.EnqueueAsync(WatchlistOperationKind.Add, [id]);

		var row = await fixture.Context.WatchlistOutbox.AsNoTracking().SingleAsync();
		await Assert.That(row.Kind).IsEqualTo(WatchlistOperationKind.Add);
	}

	[Test]
	public async Task EnqueueAsync_ExistingId_ReplacesKind()
	{
		var id = PlexId.From("plex-201", MediaType.Movie);
		await _testee.EnqueueAsync(WatchlistOperationKind.Add, [id]);

		await _testee.EnqueueAsync(WatchlistOperationKind.Remove, [id]);

		var rows = await fixture.Context.WatchlistOutbox.AsNoTracking().ToListAsync();
		await Assert.That(rows).Count().IsEqualTo(1);
		await Assert.That(rows[0].Kind).IsEqualTo(WatchlistOperationKind.Remove);
	}

	[Test]
	public async Task GetAsync_ReturnsAllOpsForProvider()
	{
		var addId = PlexId.From("plex-202", MediaType.Movie);
		var removeId = PlexId.From("plex-203", MediaType.Movie);
		await _testee.EnqueueAsync(WatchlistOperationKind.Add, [addId]);
		await _testee.EnqueueAsync(WatchlistOperationKind.Remove, [removeId]);

		var ops = await _testee.GetAsync(MediaProvider.Plex);

		await Assert.That(ops).Count().IsEqualTo(2);
		await Assert.That(ops.Single(o => o.Kind == WatchlistOperationKind.Add).MediaId).IsEqualTo<IMediaId>(addId);
		await Assert.That(ops.Single(o => o.Kind == WatchlistOperationKind.Remove).MediaId).IsEqualTo<IMediaId>(removeId);
	}

	[Test]
	public async Task DropAsync_RemovesOnlyMatchingKind()
	{
		var id = PlexId.From("plex-204", MediaType.Movie);
		await _testee.EnqueueAsync(WatchlistOperationKind.Add, [id]);

		await _testee.DropAsync(WatchlistOperationKind.Remove, [id]);

		var rows = await fixture.Context.WatchlistOutbox.AsNoTracking().ToListAsync();
		await Assert.That(rows).Count().IsEqualTo(1);
		await Assert.That(rows[0].Kind).IsEqualTo(WatchlistOperationKind.Add);
	}

	[Test]
	public async Task DropAsync_RemovesMatchingKind()
	{
		var id = PlexId.From("plex-205", MediaType.Movie);
		await _testee.EnqueueAsync(WatchlistOperationKind.Add, [id]);

		await _testee.DropAsync(WatchlistOperationKind.Add, [id]);

		var any = await fixture.Context.WatchlistOutbox.AnyAsync();
		await Assert.That(any).IsFalse();
	}

	[Test]
	public async Task EnqueueAsync_BatchOfDistinctMedia_CreatesSeparateRows()
	{
		var first = PlexId.From("plex-400", MediaType.Movie);
		var second = PlexId.From("plex-401", MediaType.Movie);

		await _testee.EnqueueAsync(WatchlistOperationKind.Add, [first, second]);

		var rows = await fixture.Context.WatchlistOutbox.AsNoTracking().ToListAsync();
		await Assert.That(rows).Count().IsEqualTo(2);
	}

	[Test]
	public async Task EnqueueAsync_EmptyInput_NoOp()
	{
		await _testee.EnqueueAsync(WatchlistOperationKind.Add, []);

		var any = await fixture.Context.WatchlistOutbox.AnyAsync();
		await Assert.That(any).IsFalse();
	}
}
