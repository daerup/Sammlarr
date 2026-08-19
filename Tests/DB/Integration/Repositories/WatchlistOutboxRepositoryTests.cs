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
	public async Task EnqueueAsync_NewId_InsertsPendingRow()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-200", MediaType.Movie)]);

		await _testee.EnqueueAsync(WatchlistOperationAction.Add, [mediaId]);

		var delivery = (await _testee.GetAsync()).Single();
		await Assert.That(delivery.Action).IsEqualTo(WatchlistOperationAction.Add);
		await Assert.That(delivery.Status).IsEqualTo(WatchlistDeliveryStatus.Pending);
	}

	[Test]
	public async Task EnqueueAsync_ExistingId_ReplacesKind()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-201", MediaType.Movie)]);
		await _testee.EnqueueAsync(WatchlistOperationAction.Add, [mediaId]);

		await _testee.EnqueueAsync(WatchlistOperationAction.Remove, [mediaId]);

		var deliveries = await _testee.GetAsync();
		await Assert.That(deliveries).Count().IsEqualTo(1);
		await Assert.That(deliveries[0].Action).IsEqualTo(WatchlistOperationAction.Remove);
	}

	[Test]
	public async Task GetAsync_ReturnsAllOps()
	{
		var addId = await SeedMediaAsync([PlexId.From("plex-202", MediaType.Movie)]);
		var removeId = await SeedMediaAsync([PlexId.From("plex-203", MediaType.Movie)]);
		await _testee.EnqueueAsync(WatchlistOperationAction.Add, [addId]);
		await _testee.EnqueueAsync(WatchlistOperationAction.Remove, [removeId]);

		var deliveries = await _testee.GetAsync();

		await Assert.That(deliveries).Count().IsEqualTo(2);
		await Assert.That(deliveries.Single(o => o.Action == WatchlistOperationAction.Add).MediaId).IsEqualTo(addId);
		await Assert.That(deliveries.Single(o => o.Action == WatchlistOperationAction.Remove).MediaId).IsEqualTo(removeId);
	}

	[Test]
	public async Task GetAsync_IncludesStatusAndAttempts()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-700", MediaType.Movie)]);
		await _testee.EnqueueAsync(WatchlistOperationAction.Add, [mediaId]);
		await _testee.RecordFailureAsync([mediaId], WatchlistDeliveryStatus.Pending);

		var delivery = (await _testee.GetAsync()).Single();

		await Assert.That(delivery.MediaId).IsEqualTo(mediaId);
		await Assert.That(delivery.Status).IsEqualTo(WatchlistDeliveryStatus.Pending);
		await Assert.That(delivery.Attempts).IsEqualTo(1);
	}

	[Test]
	public async Task FindPendingAsync_ReturnsPendingRow()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-710", MediaType.Movie)]);
		await _testee.EnqueueAsync(WatchlistOperationAction.Remove, [mediaId]);

		var delivery = await _testee.FindPendingAsync(mediaId);

		await Assert.That(delivery).IsNotNull();
		await Assert.That(delivery!.Action).IsEqualTo(WatchlistOperationAction.Remove);
	}

	[Test]
	public async Task FindPendingAsync_ReturnsNullForFailedRow()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-711", MediaType.Movie)]);
		await _testee.EnqueueAsync(WatchlistOperationAction.Add, [mediaId]);
		await _testee.RecordFailureAsync([mediaId], WatchlistDeliveryStatus.Failed);

		var delivery = await _testee.FindPendingAsync(mediaId);

		await Assert.That(delivery).IsNull();
	}

	[Test]
	public async Task FindPendingAsync_ReturnsNullWhenAbsent()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-712", MediaType.Movie)]);

		var delivery = await _testee.FindPendingAsync(mediaId);

		await Assert.That(delivery).IsNull();
	}

	[Test]
	public async Task DropAsync_RemovesEntry()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-205", MediaType.Movie)]);
		await _testee.EnqueueAsync(WatchlistOperationAction.Add, [mediaId]);

		await _testee.DropAsync([mediaId]);

		var any = await fixture.Context.WatchlistOutbox.AnyAsync();
		await Assert.That(any).IsFalse();
	}

	[Test]
	public async Task EnqueueAsync_BatchOfDistinctMedia_CreatesSeparateRows()
	{
		var first = await SeedMediaAsync([PlexId.From("plex-400", MediaType.Movie)]);
		var second = await SeedMediaAsync([PlexId.From("plex-401", MediaType.Movie)]);

		await _testee.EnqueueAsync(WatchlistOperationAction.Add, [first, second]);

		var rows = await fixture.Context.WatchlistOutbox.AsNoTracking().ToListAsync();
		await Assert.That(rows).Count().IsEqualTo(2);
	}

	[Test]
	public async Task EnqueueAsync_EmptyInput_NoOp()
	{
		await _testee.EnqueueAsync(WatchlistOperationAction.Add, []);

		var any = await fixture.Context.WatchlistOutbox.AnyAsync();
		await Assert.That(any).IsFalse();
	}

	[Test]
	public async Task RecordFailureAsync_TransientIncrementsAttemptsAndKeepsPending()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-500", MediaType.Movie)]);
		await _testee.EnqueueAsync(WatchlistOperationAction.Add, [mediaId]);

		await _testee.RecordFailureAsync([mediaId], WatchlistDeliveryStatus.Pending);
		await _testee.RecordFailureAsync([mediaId], WatchlistDeliveryStatus.Pending);

		var delivery = (await _testee.GetAsync()).Single();
		await Assert.That(delivery.Attempts).IsEqualTo(2);
		await Assert.That(delivery.Status).IsEqualTo(WatchlistDeliveryStatus.Pending);
	}

	[Test]
	public async Task RecordFailureAsync_GiveUpMarksFailedAndRoundTrips()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-701", MediaType.Movie)]);
		await _testee.EnqueueAsync(WatchlistOperationAction.Add, [mediaId]);

		await _testee.RecordFailureAsync([mediaId], WatchlistDeliveryStatus.Failed);

		var delivery = (await _testee.GetAsync()).Single();
		await Assert.That(delivery.Status).IsEqualTo(WatchlistDeliveryStatus.Failed);
		await Assert.That(delivery.Attempts).IsEqualTo(1);
	}

	[Test]
	public async Task RecordFailureAsync_EmptyInput_NoOp()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-502", MediaType.Movie)]);
		await _testee.EnqueueAsync(WatchlistOperationAction.Add, [mediaId]);

		await _testee.RecordFailureAsync([], WatchlistDeliveryStatus.Failed);

		var delivery = (await _testee.GetAsync()).Single();
		await Assert.That(delivery.Attempts).IsEqualTo(0);
		await Assert.That(delivery.Status).IsEqualTo(WatchlistDeliveryStatus.Pending);
	}

	[Test]
	public async Task EnqueueAsync_ExistingId_ResetsStatusToPendingAndAttemptsToZero()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-702", MediaType.Movie)]);
		await _testee.EnqueueAsync(WatchlistOperationAction.Add, [mediaId]);
		await _testee.RecordFailureAsync([mediaId], WatchlistDeliveryStatus.Failed);

		await _testee.EnqueueAsync(WatchlistOperationAction.Add, [mediaId]);

		var delivery = (await _testee.GetAsync()).Single();
		await Assert.That(delivery.Status).IsEqualTo(WatchlistDeliveryStatus.Pending);
		await Assert.That(delivery.Attempts).IsEqualTo(0);
	}
}
