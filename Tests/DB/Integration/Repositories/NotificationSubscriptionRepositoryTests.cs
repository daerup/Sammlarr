using Contracts.Notifications;
using DB.Data.Notifications;
using Domain.Notifications;
using Shared;
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

		await _testee.AddAsync(targetId, ["watchlist.synced"], NotificationTemplate.None);

		var byTarget = await _testee.GetForTargetAsync(targetId);
		var byEventType = await _testee.GetForEventTypeAsync("watchlist.synced");

		await Assert.That(byTarget).Count().IsEqualTo(1);
		await Assert.That(byTarget[0].EventTypes).Contains("watchlist.synced");
		await Assert.That(byEventType).Count().IsEqualTo(1);
		await Assert.That(byEventType[0].TargetId).IsEqualTo(targetId);
	}

	[Test]
	public async Task A_bundled_subscription_matches_each_of_its_events()
	{
		var targetId = await CreateTargetAsync();

		await _testee.AddAsync(targetId, ["watchlist.synced", "watchlist.items.added"], NotificationTemplate.None);

		await Assert.That(await _testee.GetForEventTypeAsync("watchlist.synced")).Count().IsEqualTo(1);
		await Assert.That(await _testee.GetForEventTypeAsync("watchlist.items.added")).Count().IsEqualTo(1);
		await Assert.That(await _testee.GetForEventTypeAsync("feed.run.failed")).IsEmpty();
	}

	[Test]
	public async Task Subscription_template_and_priority_round_trip_and_update()
	{
		var targetId = await CreateTargetAsync();
		var id = await _testee.AddAsync(targetId, ["watchlist.synced"], new NotificationTemplate(Some.Of("{{ title }}"), new None(), new None(), Some.Of(1)));

		var created = (await _testee.GetForTargetAsync(targetId)).Single();
		await Assert.That(created.Template.Body.Match(value => value, () => null)).IsEqualTo("{{ title }}");
		await Assert.That(created.Template.Priority.Match(value => (int?)value, () => null)).IsEqualTo(1);

		await _testee.UpdateAsync(id, ["watchlist.synced"], new NotificationTemplate(Some.Of("{{ summary }}"), new None(), new None(), new None()));

		var updated = (await _testee.GetForTargetAsync(targetId)).Single();
		await Assert.That(updated.Template.Body.Match(value => value, () => null)).IsEqualTo("{{ summary }}");
		await Assert.That(updated.Template.Priority.Match(value => (int?)value, () => null)).IsNull();
	}

	[Test]
	public async Task Deleting_a_target_cascades_its_subscriptions()
	{
		var targetId = await CreateTargetAsync();
		await _testee.AddAsync(targetId, ["watchlist.items.added"], NotificationTemplate.None);

		await _targets.DeleteAsync(targetId);

		await Assert.That(await _testee.GetForTargetAsync(targetId)).IsEmpty();
	}
}
