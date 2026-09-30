using Contracts.Wantlist.Outbox;
using Dapper;
using DB.Data.Feed;
using DB.Data.Jobs;
using DB.Data.Notifications;
using DB.Data.Wantlist;
using Domain.Feed;
using Domain.Jobs;
using Domain.Notifications;
using Domain.Processing;
using Domain.Providers;
using Domain.Wantlist;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;
using Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Integration.Tests.Persistence;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class RenameWantlistSinkBlocklistMigrationTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string BeforeTheRename = "20260930063931_FeedRunSourceFailure";

	private static readonly QueryLimit Limit = new()
	{
		Count = 5,
		Wantlisted = SlotPolicy.Preserved,
		Blocklisted = SlotPolicy.Preserved,
	};

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task RollingBack_StoresEveryRenamedValueUnderItsOldName()
	{
		await SeedAsync();

		await MigrateAsync(BeforeTheRename);

		await Assert.That(await ScalarAsync("""SELECT "Origin" || ':' || "RemoteProvider" FROM "Watchlist" """)).IsEqualTo("Remote:Plex");
		await Assert.That(await ScalarAsync("""SELECT count(*) FROM "Blacklist" """)).IsEqualTo("1");
		await Assert.That(await ScalarAsync("""SELECT "Action" FROM "WatchlistOutbox" """)).IsEqualTo("Remove");
		await Assert.That(await ScalarAsync("""SELECT "WatchlistProvider" FROM "ProviderConfig" """)).IsEqualTo("Plex");
		await Assert.That(await ScalarAsync("""SELECT "Kind" FROM "Jobs" """)).IsEqualTo("WatchlistSync");
		await Assert.That(await ScalarAsync("""SELECT "Limit" FROM "Feeds" """)).IsEqualTo("""{"Count":5,"Watchlisted":"Preserved","Blacklisted":"Preserved"}""");
		await Assert
			.That(await ScalarAsync("""SELECT "EventTypes" FROM "NotificationSubscriptions" """))
			.IsEqualTo("""["WatchlistSyncCompleted","FeedRunFailed"]""");
		await Assert.That(await ScalarAsync("""SELECT "EventType" FROM "NotificationDeliveryOutcomes" """)).IsEqualTo("WatchlistSyncFailed");
	}

	[Test]
	public async Task Reapplying_ReadsBackEveryRenamedValue()
	{
		await SeedAsync();
		await MigrateAsync(BeforeTheRename);

		await MigrateAsync(null);

		var context = host.Context;
		context.ChangeTracker.Clear();
		var item = await context.Wantlist.AsNoTracking().SingleAsync();
		await Assert.That((item.Origin, item.Sink)).IsEqualTo((WantlistItemOrigin.Sink, SinkKind.Plex));
		await Assert.That(await context.Blocklist.CountAsync()).IsEqualTo(1);
		await Assert.That((await context.WantlistOutbox.AsNoTracking().SingleAsync()).Action).IsEqualTo(WantlistOperationAction.Remove);
		await Assert.That((await context.ProviderSettings.AsNoTracking().SingleAsync()).Sink).IsEqualTo(SinkKind.Plex);
		await Assert.That((await context.Jobs.AsNoTracking().SingleAsync()).Kind).IsEqualTo(JobKind.WantlistSync);
		await Assert.That((await context.Feeds.AsNoTracking().SingleAsync()).Limit).IsEqualTo(Limit);
		await Assert
			.That((await context.NotificationSubscriptions.AsNoTracking().SingleAsync()).EventTypes)
			.IsEquivalentTo([NotificationEventType.WantlistSyncCompleted, NotificationEventType.FeedRunFailed]);
		await Assert.That((await context.NotificationDeliveryOutcomes.AsNoTracking().SingleAsync()).EventType).IsEqualTo(NotificationEventType.WantlistSyncFailed);
	}

	private async Task SeedAsync()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWantlist(new WantlistItemProvenance(new SinkOrigin(SinkKind.Plex)));
		await host.Movie(TestMedia.FightClub).OnBlocklist();
		await host.Resolve<IWantlistOutbox>().EnqueueAsync(WantlistOperationAction.Remove, [inception]);

		var context = host.Context;
		var target = new NotificationTargetRow { Name = "Phone", Channel = NotificationChannel.Webhook };
		context.NotificationTargets.Add(target);
		await context.SaveChangesAsync();
		context.NotificationSubscriptions.Add(
			new NotificationSubscriptionRow { TargetId = target.Id, EventTypes = [NotificationEventType.WantlistSyncCompleted, NotificationEventType.FeedRunFailed] }
		);
		context.NotificationDeliveryOutcomes.Add(
			new NotificationDeliveryOutcomeRow
			{
				EventId = Guid.NewGuid(),
				TargetId = target.Id,
				EventType = NotificationEventType.WantlistSyncFailed,
				Kind = OutcomeKind.Delivered,
			}
		);
		context.Feeds.Add(
			new FeedRow
			{
				Name = "Weekly picks",
				IsEnabled = true,
				Provider = FeedProvider.Mdblist,
				Strategy = new FeedStrategy(new Append()),
				Identifier = "2236",
				Limit = Limit,
			}
		);
		context.Jobs.Add(
			new JobRow
			{
				Id = Guid.NewGuid(),
				OwnerId = SammlarrTestHost.SeededAdmin.Id,
				Kind = JobKind.WantlistSync,
				Key = string.Empty,
				Request = new SyncWantlist(new ManualTrigger()),
				State = JobState.Queued,
				RunAfter = DateTimeOffset.UnixEpoch,
				Deadline = DateTimeOffset.UnixEpoch,
			}
		);
		await context.SaveChangesAsync();
		context.ChangeTracker.Clear();
	}

	private Task MigrateAsync(string? target) => host.Context.GetService<IMigrator>().MigrateAsync(target);

	private async Task<string?> ScalarAsync(string sql) => await host.Connection.ExecuteScalarAsync<string>(sql);
}
