using Contracts.Notifications;
using DB.Data.Notifications;
using Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace DB.Tests.Integration.Repositories;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class NotificationSubscriptionRepositoryTests(SQLiteFixture fixture)
{
	private INotificationTargetRepository _targets = null!;
	private INotificationSubscriptionRepository _testee = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup()
	{
		_targets = new NotificationTargetRepository(fixture.ContextFactory);
		_testee = new NotificationSubscriptionRepository(fixture.ContextFactory);
	}

	private async Task<Guid> CreateTargetAsync() =>
		await _targets.CreateAsync(new NotificationTarget(Guid.Empty, "Target", true, new PushoverConfig("token", "key")));

	[Test]
	public async Task Subscription_is_readable_by_target_and_by_event_type()
	{
		var targetId = await CreateTargetAsync();

		await _testee.AddAsync(targetId, "watchlist.synced");

		var byTarget = await _testee.GetForTargetAsync(targetId);
		var byEventType = await _testee.GetForEventTypeAsync("watchlist.synced");

		await Assert.That(byTarget).Count().IsEqualTo(1);
		await Assert.That(byTarget[0].EventType).IsEqualTo("watchlist.synced");
		await Assert.That(byEventType).Count().IsEqualTo(1);
		await Assert.That(byEventType[0].TargetId).IsEqualTo(targetId);
	}

	[Test]
	public async Task Deleting_a_target_cascades_its_subscriptions()
	{
		var targetId = await CreateTargetAsync();
		await _testee.AddAsync(targetId, "watchlist.items.added");

		await _targets.DeleteAsync(targetId);

		await Assert.That(await _testee.GetForTargetAsync(targetId)).IsEmpty();
	}

	[Test]
	public async Task Duplicate_subscription_for_same_target_and_event_is_rejected()
	{
		var targetId = await CreateTargetAsync();
		await _testee.AddAsync(targetId, "watchlist.synced");

		var threw = false;
		try
		{
			await _testee.AddAsync(targetId, "watchlist.synced");
		}
		catch (DbUpdateException)
		{
			threw = true;
		}

		await Assert.That(threw).IsTrue();
	}
}
