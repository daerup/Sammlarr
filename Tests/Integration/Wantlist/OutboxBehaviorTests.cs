using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using Domain.Wantlist.Outbox;
using Harness;

namespace Integration.Tests.Wantlist;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class OutboxBehaviorTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly WantlistItemProvenance Manual = new(new ManualOrigin(ManualSource.Unknown));
	private static readonly WantlistItemProvenance Remote = new(new SinkOrigin(SinkKind.Plex));

	private IWantlistService Wantlist => host.Resolve<IWantlistService>();
	private IWantlistOutbox Outbox => host.Resolve<IWantlistOutbox>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task AddAsync_WhenTheItemIsNew_EnqueuesAnAdd()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();

		await Wantlist.AddAsync([inception], Manual);

		await AssertOutbox((WantlistOperationAction.Add, inception));
	}

	[Test]
	public async Task AddAsync_WhenARemoveIsPending_CancelsTheRemove()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWantlist(origin: Remote);
		await Outbox.EnqueueAsync(WantlistOperationAction.Remove, [inception]);

		await Wantlist.AddAsync([inception], Manual);

		await AssertOutboxEmpty();
	}

	[Test]
	public async Task AddAsync_WhenTheCollectionIsEmpty_LeavesTheOutboxUntouched()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await Outbox.EnqueueAsync(WantlistOperationAction.Add, [inception]);

		await Wantlist.AddAsync([], Manual);

		await AssertOutbox((WantlistOperationAction.Add, inception));
	}

	[Test]
	public async Task AddAsync_WhenTheItemIsAlreadySynced_EnqueuesNothing()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWantlist(origin: Remote);

		await Wantlist.AddAsync([inception], Manual);

		await AssertOutboxEmpty();
	}

	[Test]
	public async Task AddAsync_WhenAnAddIsAlreadyPending_DoesNotDuplicateIt()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await Outbox.EnqueueAsync(WantlistOperationAction.Add, [inception]);

		await Wantlist.AddAsync([inception], Manual);

		await AssertOutbox((WantlistOperationAction.Add, inception));
	}

	[Test]
	public async Task RemoveAsync_WhenTheItemIsSynced_EnqueuesARemove()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWantlist(origin: Remote);

		await Wantlist.RemoveAsync([inception]);

		await AssertOutbox((WantlistOperationAction.Remove, inception));
	}

	[Test]
	public async Task RemoveAsync_WhenAnAddIsPending_CancelsTheAdd()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWantlist(origin: Remote);
		await Outbox.EnqueueAsync(WantlistOperationAction.Add, [inception]);

		await Wantlist.RemoveAsync([inception]);

		await AssertOutboxEmpty();
	}

	[Test]
	public async Task RemoveAsync_WhenARemoveIsAlreadyPending_DoesNotDuplicateIt()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWantlist(origin: Remote);
		await Outbox.EnqueueAsync(WantlistOperationAction.Remove, [inception]);

		await Wantlist.RemoveAsync([inception]);

		await AssertOutbox((WantlistOperationAction.Remove, inception));
	}

	[Test]
	public async Task RemoveAsync_WhenTheCollectionIsEmpty_LeavesTheOutboxUntouched()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		await Outbox.EnqueueAsync(WantlistOperationAction.Add, [inception]);

		await Wantlist.RemoveAsync([]);

		await AssertOutbox((WantlistOperationAction.Add, inception));
	}

	[Test]
	public async Task AddAsync_WhenTheSameItemAppearsTwice_EnqueuesASingleAdd()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();

		await Wantlist.AddAsync([inception, inception], Manual);

		await AssertOutbox((WantlistOperationAction.Add, inception));
	}

	[Test]
	public async Task AddAsync_WhenTheBatchIsMixed_PartitionsCancelsPushesAndNoOps()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWantlist(origin: Remote);
		var fightClub = await host.Movie(TestMedia.FightClub).SeedAsync();
		var matrix = await host.Movie("The Matrix", tmdb: "603", plex: "603").OnWantlist(origin: Remote);
		await Outbox.EnqueueAsync(WantlistOperationAction.Remove, [inception]);

		await Wantlist.AddAsync([inception, fightClub, matrix], Manual);

		await AssertOutbox((WantlistOperationAction.Add, fightClub));
	}

	private async Task AssertOutbox(params (WantlistOperationAction Action, MediaId MediaId)[] expected)
	{
		var entries = await Outbox.GetAllAsync();
		await Assert.That(entries).Count().IsEqualTo(expected.Length);
		foreach (var (action, mediaId) in expected)
		{
			await Assert.That(entries.Any(entry => entry.Action == action && entry.MediaId == mediaId)).IsTrue();
		}
	}

	private async Task AssertOutboxEmpty() => await Assert.That(await Outbox.GetAllAsync()).IsEmpty();
}
