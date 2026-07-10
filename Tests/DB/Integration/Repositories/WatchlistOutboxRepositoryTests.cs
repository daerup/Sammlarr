using Contracts.Media;
using DB.Data.Media;
using DB.Data.Watchlist.Outbox;
using Domain.Media;
using Domain.Watchlist.Outbox;
using Microsoft.EntityFrameworkCore;

namespace DB.Tests.Integration.Repositories;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class WatchlistOutboxRepositoryTests(SQLiteFixture fixture) : IAsyncDisposable
{
	private WatchlistOutboxRepository _testee = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup() => _testee = new WatchlistOutboxRepository(fixture.ContextFactory);

	private async Task<MediaId> SeedMediaAsync(IEnumerable<IExternalMediaId> ids)
	{
		var media = new MediaRow();
		await fixture.Context.ExternalMediaIds.AddRangeAsync(ids.Select(id => id.ToExternalIdEntity(media)));
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return MediaId.From(media.Id);
	}

	[Test]
	public async Task EnqueueAsync_NewId_InsertsRow()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-200", MediaType.Movie)]);

		await _testee.EnqueueAsync(WatchlistOperationKind.Add, [mediaId]);

		var row = await fixture.Context.WatchlistOutbox.AsNoTracking().SingleAsync();
		await Assert.That(row.Kind).IsEqualTo(WatchlistOperationKind.Add);
	}

	[Test]
	public async Task EnqueueAsync_ExistingId_ReplacesKind()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-201", MediaType.Movie)]);
		await _testee.EnqueueAsync(WatchlistOperationKind.Add, [mediaId]);

		await _testee.EnqueueAsync(WatchlistOperationKind.Remove, [mediaId]);

		var rows = await fixture.Context.WatchlistOutbox.AsNoTracking().ToListAsync();
		await Assert.That(rows).Count().IsEqualTo(1);
		await Assert.That(rows[0].Kind).IsEqualTo(WatchlistOperationKind.Remove);
	}

	[Test]
	public async Task GetAsync_ReturnsAllOps()
	{
		var addId = await SeedMediaAsync([PlexId.From("plex-202", MediaType.Movie)]);
		var removeId = await SeedMediaAsync([PlexId.From("plex-203", MediaType.Movie)]);
		await _testee.EnqueueAsync(WatchlistOperationKind.Add, [addId]);
		await _testee.EnqueueAsync(WatchlistOperationKind.Remove, [removeId]);

		var ops = await _testee.GetAsync();

		await Assert.That(ops).Count().IsEqualTo(2);
		await Assert.That(ops.Single(o => o.Kind == WatchlistOperationKind.Add).MediaId).IsEqualTo(addId);
		await Assert.That(ops.Single(o => o.Kind == WatchlistOperationKind.Remove).MediaId).IsEqualTo(removeId);
	}

	[Test]
	public async Task DropAsync_RemovesEntry()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-205", MediaType.Movie)]);
		await _testee.EnqueueAsync(WatchlistOperationKind.Add, [mediaId]);

		await _testee.DropAsync(WatchlistOperationKind.Add, [mediaId]);

		var any = await fixture.Context.WatchlistOutbox.AnyAsync();
		await Assert.That(any).IsFalse();
	}

	[Test]
	public async Task EnqueueAsync_BatchOfDistinctMedia_CreatesSeparateRows()
	{
		var first = await SeedMediaAsync([PlexId.From("plex-400", MediaType.Movie)]);
		var second = await SeedMediaAsync([PlexId.From("plex-401", MediaType.Movie)]);

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

	[Test]
	public async Task RecordFailureAsync_IncrementsAttemptsAndKeepsRow()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-500", MediaType.Movie)]);
		await _testee.EnqueueAsync(WatchlistOperationKind.Add, [mediaId]);

		await _testee.RecordFailureAsync(WatchlistOperationKind.Add, [mediaId]);
		await _testee.RecordFailureAsync(WatchlistOperationKind.Add, [mediaId]);

		fixture.Context.ChangeTracker.Clear();
		var row = await fixture.Context.WatchlistOutbox.AsNoTracking().SingleAsync();
		await Assert.That(row.Attempts).IsEqualTo(2);
	}

	[Test]
	public async Task RecordFailureAsync_EmptyInput_NoOp()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-502", MediaType.Movie)]);
		await _testee.EnqueueAsync(WatchlistOperationKind.Add, [mediaId]);

		await _testee.RecordFailureAsync(WatchlistOperationKind.Add, []);

		fixture.Context.ChangeTracker.Clear();
		var row = await fixture.Context.WatchlistOutbox.AsNoTracking().SingleAsync();
		await Assert.That(row.Attempts).IsEqualTo(0);
	}

	[Test]
	public async Task DropAsync_KindMismatch_LeavesRowAlone()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-600", MediaType.Movie)]);
		await _testee.EnqueueAsync(WatchlistOperationKind.Remove, [mediaId]);

		await _testee.DropAsync(WatchlistOperationKind.Add, [mediaId]);

		var row = await fixture.Context.WatchlistOutbox.AsNoTracking().SingleAsync();
		await Assert.That(row.Kind).IsEqualTo(WatchlistOperationKind.Remove);
	}

	[Test]
	public async Task RecordFailureAsync_KindMismatch_LeavesAttemptsAlone()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-601", MediaType.Movie)]);
		await _testee.EnqueueAsync(WatchlistOperationKind.Remove, [mediaId]);

		await _testee.RecordFailureAsync(WatchlistOperationKind.Add, [mediaId]);

		fixture.Context.ChangeTracker.Clear();
		var row = await fixture.Context.WatchlistOutbox.AsNoTracking().SingleAsync();
		await Assert.That(row.Attempts).IsEqualTo(0);
	}
}
