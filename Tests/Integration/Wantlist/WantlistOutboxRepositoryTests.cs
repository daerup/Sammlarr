using DB.Data.Media;
using DB.Data.Wantlist.Outbox;
using Domain.Media;
using Domain.Wantlist.Outbox;
using Harness;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Wantlist;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class WantlistOutboxRepositoryTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private WantlistOutboxRepository _testee = null!;

	[Before(Test)]
	public void Setup() => _testee = new WantlistOutboxRepository(fixture.ContextFactory);

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

		await _testee.EnqueueAsync(WantlistOperationAction.Add, [mediaId]);

		var entry = (await _testee.GetAsync()).Single();
		await Assert.That(entry.Action).IsEqualTo(WantlistOperationAction.Add);
		await Assert.That(entry.Status).IsEqualTo(WantlistOutboxState.Pending);
	}

	[Test]
	public async Task EnqueueAsync_ExistingId_ReplacesKind()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-201", MediaType.Movie)]);
		await _testee.EnqueueAsync(WantlistOperationAction.Add, [mediaId]);

		await _testee.EnqueueAsync(WantlistOperationAction.Remove, [mediaId]);

		var deliveries = await _testee.GetAsync();
		await Assert.That(deliveries).Count().IsEqualTo(1);
		await Assert.That(deliveries[0].Action).IsEqualTo(WantlistOperationAction.Remove);
	}

	[Test]
	public async Task GetAsync_ReturnsAllOps()
	{
		var addId = await SeedMediaAsync([PlexId.From("plex-202", MediaType.Movie)]);
		var removeId = await SeedMediaAsync([PlexId.From("plex-203", MediaType.Movie)]);
		await _testee.EnqueueAsync(WantlistOperationAction.Add, [addId]);
		await _testee.EnqueueAsync(WantlistOperationAction.Remove, [removeId]);

		var deliveries = await _testee.GetAsync();

		await Assert.That(deliveries).Count().IsEqualTo(2);
		await Assert.That(deliveries.Single(o => o.Action == WantlistOperationAction.Add).MediaId).IsEqualTo(addId);
		await Assert.That(deliveries.Single(o => o.Action == WantlistOperationAction.Remove).MediaId).IsEqualTo(removeId);
	}

	[Test]
	public async Task GetAsync_IncludesStatusAndAttempts()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-700", MediaType.Movie)]);
		await _testee.EnqueueAsync(WantlistOperationAction.Add, [mediaId]);
		await _testee.RecordFailedAttemptAsync([mediaId], WantlistOutboxState.Pending);

		var entry = (await _testee.GetAsync()).Single();

		await Assert.That(entry.MediaId).IsEqualTo(mediaId);
		await Assert.That(entry.Status).IsEqualTo(WantlistOutboxState.Pending);
		await Assert.That(entry.Attempts).IsEqualTo(1);
	}

	[Test]
	public async Task FindPendingAsync_ReturnsPendingRow()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-710", MediaType.Movie)]);
		await _testee.EnqueueAsync(WantlistOperationAction.Remove, [mediaId]);

		var entry = await _testee.FindPendingAsync(mediaId);

		await Assert.That(entry).IsNotNull();
		await Assert.That(entry!.Action).IsEqualTo(WantlistOperationAction.Remove);
	}

	[Test]
	public async Task FindPendingAsync_ReturnsNullForFailedRow()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-711", MediaType.Movie)]);
		await _testee.EnqueueAsync(WantlistOperationAction.Add, [mediaId]);
		await _testee.RecordFailedAttemptAsync([mediaId], WantlistOutboxState.Failed);

		var entry = await _testee.FindPendingAsync(mediaId);

		await Assert.That(entry).IsNull();
	}

	[Test]
	public async Task FindPendingAsync_ReturnsNullWhenAbsent()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-712", MediaType.Movie)]);

		var entry = await _testee.FindPendingAsync(mediaId);

		await Assert.That(entry).IsNull();
	}

	[Test]
	public async Task DropAsync_RemovesEntry()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-205", MediaType.Movie)]);
		await _testee.EnqueueAsync(WantlistOperationAction.Add, [mediaId]);

		await _testee.DropAsync([mediaId]);

		var any = await fixture.Context.WantlistOutbox.AnyAsync();
		await Assert.That(any).IsFalse();
	}

	[Test]
	public async Task EnqueueAsync_EmptyInput_NoOp()
	{
		await _testee.EnqueueAsync(WantlistOperationAction.Add, []);

		var any = await fixture.Context.WantlistOutbox.AnyAsync();
		await Assert.That(any).IsFalse();
	}

	[Test]
	public async Task RecordFailedAttemptAsync_TransientIncrementsAttemptsAndKeepsPending()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-500", MediaType.Movie)]);
		await _testee.EnqueueAsync(WantlistOperationAction.Add, [mediaId]);

		await _testee.RecordFailedAttemptAsync([mediaId], WantlistOutboxState.Pending);
		await _testee.RecordFailedAttemptAsync([mediaId], WantlistOutboxState.Pending);

		var entry = (await _testee.GetAsync()).Single();
		await Assert.That(entry.Attempts).IsEqualTo(2);
		await Assert.That(entry.Status).IsEqualTo(WantlistOutboxState.Pending);
	}

	[Test]
	public async Task RecordFailedAttemptAsync_GiveUpMarksFailedAndRoundTrips()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-701", MediaType.Movie)]);
		await _testee.EnqueueAsync(WantlistOperationAction.Add, [mediaId]);

		await _testee.RecordFailedAttemptAsync([mediaId], WantlistOutboxState.Failed);

		var entry = (await _testee.GetAsync()).Single();
		await Assert.That(entry.Status).IsEqualTo(WantlistOutboxState.Failed);
		await Assert.That(entry.Attempts).IsEqualTo(1);
	}

	[Test]
	public async Task RecordFailedAttemptAsync_EmptyInput_NoOp()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-502", MediaType.Movie)]);
		await _testee.EnqueueAsync(WantlistOperationAction.Add, [mediaId]);

		await _testee.RecordFailedAttemptAsync([], WantlistOutboxState.Failed);

		var entry = (await _testee.GetAsync()).Single();
		await Assert.That(entry.Attempts).IsEqualTo(0);
		await Assert.That(entry.Status).IsEqualTo(WantlistOutboxState.Pending);
	}

	[Test]
	public async Task EnqueueAsync_ExistingId_ResetsStatusToPendingAndAttemptsToZero()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-702", MediaType.Movie)]);
		await _testee.EnqueueAsync(WantlistOperationAction.Add, [mediaId]);
		await _testee.RecordFailedAttemptAsync([mediaId], WantlistOutboxState.Failed);

		await _testee.EnqueueAsync(WantlistOperationAction.Add, [mediaId]);

		var entry = (await _testee.GetAsync()).Single();
		await Assert.That(entry.Status).IsEqualTo(WantlistOutboxState.Pending);
		await Assert.That(entry.Attempts).IsEqualTo(0);
	}
}
