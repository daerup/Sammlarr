using DB.Data.Wantlist.Outbox;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Outbox;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Wantlist;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class WantlistOutboxRepositoryTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private WantlistOutboxRepository _testee = null!;

	[Before(Test)]
	public void Setup() => _testee = new WantlistOutboxRepository(fixture.ContextFactory);

	[Test]
	public async Task EnqueueAsync_NewId_InsertsPendingRow()
	{
		var mediaId = await fixture.SeedMediaAsync([PlexId.From("plex-200", MediaType.Movie)]);

		await _testee.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Add, [mediaId]);

		var entry = (await _testee.GetAllAsync(SinkKind.Plex)).Single();
		await Assert.That(entry.Action).IsEqualTo(WantlistOperationAction.Add);
		await Assert.That(entry.Status).IsEqualTo(WantlistOutboxState.Pending);
	}

	[Test]
	public async Task EnqueueAsync_ExistingId_ReplacesKind()
	{
		var mediaId = await fixture.SeedMediaAsync([PlexId.From("plex-201", MediaType.Movie)]);
		await _testee.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Add, [mediaId]);

		await _testee.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Remove, [mediaId]);

		var deliveries = await _testee.GetAllAsync(SinkKind.Plex);
		await Assert.That(deliveries).Count().IsEqualTo(1);
		await Assert.That(deliveries[0].Action).IsEqualTo(WantlistOperationAction.Remove);
	}

	[Test]
	public async Task GetAllAsync_ReturnsAllOps()
	{
		var addId = await fixture.SeedMediaAsync([PlexId.From("plex-202", MediaType.Movie)]);
		var removeId = await fixture.SeedMediaAsync([PlexId.From("plex-203", MediaType.Movie)]);
		await _testee.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Add, [addId]);
		await _testee.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Remove, [removeId]);

		var deliveries = await _testee.GetAllAsync(SinkKind.Plex);

		await Assert.That(deliveries).Count().IsEqualTo(2);
		await Assert.That(deliveries.Single(o => o.Action == WantlistOperationAction.Add).MediaId).IsEqualTo(addId);
		await Assert.That(deliveries.Single(o => o.Action == WantlistOperationAction.Remove).MediaId).IsEqualTo(removeId);
	}

	[Test]
	public async Task GetAllAsync_ForAnotherSink_LeavesOutTheRowsQueuedForThisOne()
	{
		var mediaId = await fixture.SeedMediaAsync([PlexId.From("plex-206", MediaType.Movie)]);
		await _testee.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Remove, [mediaId]);

		await Assert.That(await _testee.GetAllAsync(SinkKind.Arr)).IsEmpty();
		await Assert.That((await _testee.GetAllAsync(SinkKind.Plex)).Single().MediaId).IsEqualTo(mediaId);
	}

	[Test]
	public async Task ReplaceAsync_ForAnotherSink_QueuesItsAddsAndLeavesTheOtherSinksRowsAlone()
	{
		var removed = await fixture.SeedMediaAsync([PlexId.From("plex-207", MediaType.Movie)]);
		var wanted = await fixture.SeedMediaAsync([PlexId.From("plex-208", MediaType.Movie)]);
		await _testee.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Remove, [removed]);
		await _testee.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Add, [wanted]);

		await _testee.ReplaceAsync(SinkKind.Arr, [wanted]);

		var queued = (await _testee.GetAllAsync(SinkKind.Arr)).Single();
		await Assert.That(queued.MediaId).IsEqualTo(wanted);
		await Assert.That(queued.Action).IsEqualTo(WantlistOperationAction.Add);
		await Assert
			.That((await _testee.GetAllAsync(SinkKind.Plex)).Select(entry => entry.Action))
			.IsEquivalentTo([WantlistOperationAction.Remove, WantlistOperationAction.Add]);
	}

	[Test]
	public async Task ReplaceAsync_Again_KeepsOnlyTheLatestAdds()
	{
		var earlier = await fixture.SeedMediaAsync([PlexId.From("plex-209", MediaType.Movie)]);
		var later = await fixture.SeedMediaAsync([PlexId.From("plex-210", MediaType.Movie)]);
		await _testee.ReplaceAsync(SinkKind.Arr, [earlier]);

		await _testee.ReplaceAsync(SinkKind.Arr, [later]);

		await Assert.That((await _testee.GetAllAsync(SinkKind.Arr)).Single().MediaId).IsEqualTo(later);
	}

	[Test]
	public async Task DropOtherSinksAsync_KeepsOnlyTheRowsOfTheGivenSink()
	{
		var removed = await fixture.SeedMediaAsync([PlexId.From("plex-211", MediaType.Movie)]);
		var wanted = await fixture.SeedMediaAsync([PlexId.From("plex-212", MediaType.Movie)]);
		await _testee.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Remove, [removed]);
		await _testee.ReplaceAsync(SinkKind.Arr, [wanted]);

		await _testee.DropOtherSinksAsync(SinkKind.Arr);

		await Assert.That(await fixture.Context.WantlistOutbox.CountAsync()).IsEqualTo(1);
		await Assert.That((await _testee.GetAllAsync(SinkKind.Arr)).Single().MediaId).IsEqualTo(wanted);
	}

	[Test]
	public async Task DropAsync_ForOneSink_LeavesTheSameTitlesRowForTheOtherSink()
	{
		var mediaId = await fixture.SeedMediaAsync([PlexId.From("plex-213", MediaType.Movie)]);
		await _testee.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Add, [mediaId]);
		await _testee.EnqueueAsync(SinkKind.Arr, WantlistOperationAction.Add, [mediaId]);

		await _testee.DropAsync(SinkKind.Arr, [mediaId]);

		await Assert.That(await _testee.GetAllAsync(SinkKind.Arr)).IsEmpty();
		await Assert.That((await _testee.GetAllAsync(SinkKind.Plex)).Single().MediaId).IsEqualTo(mediaId);
	}

	[Test]
	public async Task RecordFailedAttemptAsync_ForOneSink_LeavesTheSameTitlesRowForTheOtherSink()
	{
		var mediaId = await fixture.SeedMediaAsync([PlexId.From("plex-214", MediaType.Movie)]);
		await _testee.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Add, [mediaId]);
		await _testee.EnqueueAsync(SinkKind.Arr, WantlistOperationAction.Add, [mediaId]);

		await _testee.RecordFailedAttemptAsync(SinkKind.Arr, [mediaId], WantlistOutboxState.Failed, new None());

		await Assert.That((await _testee.GetAllAsync(SinkKind.Plex)).Single().Attempts).IsEqualTo(0);
		await Assert.That((await _testee.GetAllAsync(SinkKind.Arr)).Single().Attempts).IsEqualTo(1);
	}

	[Test]
	public async Task DropAsync_RemovesEntry()
	{
		var mediaId = await fixture.SeedMediaAsync([PlexId.From("plex-205", MediaType.Movie)]);
		await _testee.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Add, [mediaId]);

		await _testee.DropAsync(SinkKind.Plex, [mediaId]);

		var any = await fixture.Context.WantlistOutbox.AnyAsync();
		await Assert.That(any).IsFalse();
	}

	[Test]
	public async Task EnqueueAsync_EmptyInput_NoOp()
	{
		await _testee.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Add, []);

		var any = await fixture.Context.WantlistOutbox.AnyAsync();
		await Assert.That(any).IsFalse();
	}

	[Test]
	public async Task RecordFailedAttemptAsync_TransientIncrementsAttemptsAndKeepsPending()
	{
		var mediaId = await fixture.SeedMediaAsync([PlexId.From("plex-500", MediaType.Movie)]);
		await _testee.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Add, [mediaId]);

		await _testee.RecordFailedAttemptAsync(SinkKind.Plex, [mediaId], WantlistOutboxState.Pending, new None());
		await _testee.RecordFailedAttemptAsync(SinkKind.Plex, [mediaId], WantlistOutboxState.Pending, new None());

		var entry = (await _testee.GetAllAsync(SinkKind.Plex)).Single();
		await Assert.That(entry.Attempts).IsEqualTo(2);
		await Assert.That(entry.Status).IsEqualTo(WantlistOutboxState.Pending);
	}

	[Test]
	public async Task RecordFailedAttemptAsync_WithAReason_KeepsItAsTheLastErrorUntilTheTitleIsQueuedAgain()
	{
		var mediaId = await fixture.SeedMediaAsync([PlexId.From("plex-503", MediaType.Movie)]);
		await _testee.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Add, [mediaId]);

		await _testee.RecordFailedAttemptAsync(SinkKind.Plex, [mediaId], WantlistOutboxState.Pending, Some.Of("Plex answered 500 Internal Server Error."));
		var failed = (await _testee.GetAllAsync(SinkKind.Plex)).Single();
		await _testee.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Add, [mediaId]);

		await Assert.That(failed.LastError).IsSome().And.IsEqualTo("Plex answered 500 Internal Server Error.");
		await Assert.That((await _testee.GetAllAsync(SinkKind.Plex)).Single().LastError).IsNone();
	}

	[Test]
	public async Task RecordFailedAttemptAsync_WithoutAReason_ClearsTheEarlierOne()
	{
		var mediaId = await fixture.SeedMediaAsync([PlexId.From("plex-504", MediaType.Movie)]);
		await _testee.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Add, [mediaId]);
		await _testee.RecordFailedAttemptAsync(SinkKind.Plex, [mediaId], WantlistOutboxState.Pending, Some.Of("Plex answered 500 Internal Server Error."));

		await _testee.RecordFailedAttemptAsync(SinkKind.Plex, [mediaId], WantlistOutboxState.Pending, new None());

		await Assert.That((await _testee.GetAllAsync(SinkKind.Plex)).Single().LastError).IsNone();
	}

	[Test]
	public async Task RecordFailedAttemptAsync_GiveUpMarksFailedAndRoundTrips()
	{
		var mediaId = await fixture.SeedMediaAsync([PlexId.From("plex-701", MediaType.Movie)]);
		await _testee.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Add, [mediaId]);

		await _testee.RecordFailedAttemptAsync(SinkKind.Plex, [mediaId], WantlistOutboxState.Failed, new None());

		var entry = (await _testee.GetAllAsync(SinkKind.Plex)).Single();
		await Assert.That(entry.Status).IsEqualTo(WantlistOutboxState.Failed);
		await Assert.That(entry.Attempts).IsEqualTo(1);
	}

	[Test]
	public async Task RecordFailedAttemptAsync_EmptyInput_NoOp()
	{
		var mediaId = await fixture.SeedMediaAsync([PlexId.From("plex-502", MediaType.Movie)]);
		await _testee.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Add, [mediaId]);

		await _testee.RecordFailedAttemptAsync(SinkKind.Plex, [], WantlistOutboxState.Failed, new None());

		var entry = (await _testee.GetAllAsync(SinkKind.Plex)).Single();
		await Assert.That(entry.Attempts).IsEqualTo(0);
		await Assert.That(entry.Status).IsEqualTo(WantlistOutboxState.Pending);
	}

	[Test]
	public async Task EnqueueAsync_ExistingId_ResetsStatusToPendingAndAttemptsToZero()
	{
		var mediaId = await fixture.SeedMediaAsync([PlexId.From("plex-702", MediaType.Movie)]);
		await _testee.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Add, [mediaId]);
		await _testee.RecordFailedAttemptAsync(SinkKind.Plex, [mediaId], WantlistOutboxState.Failed, new None());

		await _testee.EnqueueAsync(SinkKind.Plex, WantlistOperationAction.Add, [mediaId]);

		var entry = (await _testee.GetAllAsync(SinkKind.Plex)).Single();
		await Assert.That(entry.Status).IsEqualTo(WantlistOutboxState.Pending);
		await Assert.That(entry.Attempts).IsEqualTo(0);
	}
}
