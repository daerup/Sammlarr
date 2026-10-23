using Contracts.Notifications;
using DB.Data.Notifications;
using Domain.Notifications;
using Harness;

namespace Integration.Tests.Notifications;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class NotificationSubscriptionRepositoryTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private INotificationTargetRepository _targets = null!;
	private INotificationSubscriptionRepository _testee = null!;

	[Before(Test)]
	public void Setup()
	{
		_targets = new NotificationTargetRepository(fixture.ContextFactory);
		_testee = new NotificationSubscriptionRepository(fixture.ContextFactory);
	}

	private async Task<Guid> CreateTargetAsync() =>
		await _targets.CreateAsync(new NotificationTarget(Guid.Empty, "Target", true, new PushoverConfig(new Secret("token"), new Secret("key"))));

	[Test]
	public async Task GetAsync_WhenASubscriptionExists_IsReadableByTargetAndEventType()
	{
		var targetId = await CreateTargetAsync();

		await _testee.AddAsync(targetId, [NotificationEventType.WantlistSyncCompleted], NotificationTemplate.None);

		var byTarget = await _testee.GetForTargetAsync(targetId);
		var byEventType = await _testee.GetForEventTypeAsync(NotificationEventType.WantlistSyncCompleted);

		await Assert.That(byTarget).Count().IsEqualTo(1);
		await Assert.That(byTarget[0].EventTypes).Contains(NotificationEventType.WantlistSyncCompleted);
		await Assert.That(byEventType).Count().IsEqualTo(1);
		await Assert.That(byEventType[0].TargetId).IsEqualTo(targetId);
	}

	[Test]
	public async Task GetByEventTypeAsync_WhenASubscriptionIsBundled_MatchesEachOfItsEvents()
	{
		var targetId = await CreateTargetAsync();

		await _testee.AddAsync(targetId, [NotificationEventType.WantlistSyncCompleted, NotificationEventType.FeedRunCompleted], NotificationTemplate.None);

		await Assert.That(await _testee.GetForEventTypeAsync(NotificationEventType.WantlistSyncCompleted)).Count().IsEqualTo(1);
		await Assert.That(await _testee.GetForEventTypeAsync(NotificationEventType.FeedRunCompleted)).Count().IsEqualTo(1);
		await Assert.That(await _testee.GetForEventTypeAsync(NotificationEventType.FeedRunFailed)).IsEmpty();
	}

	[Test]
	public async Task UpsertAsync_WhenTemplateAndLevelChange_RoundTripAndUpdate()
	{
		var targetId = await CreateTargetAsync();
		var created = new NotificationTemplate(
			Some.Of("{{ title }}"),
			new None(),
			Some.Of("{{ summary }}"),
			Some.Of(NotificationLevel.Warning),
			Some.Of(PushoverPriority.Low)
		);
		var updated = new NotificationTemplate(Some.Of("{{ summary }}"), new None(), new None(), new None(), new None());

		var id = await _testee.AddAsync(targetId, [NotificationEventType.WantlistSyncCompleted], created);
		await Assert.That((await _testee.GetForTargetAsync(targetId)).Single().Template).IsEqualTo(created);

		await _testee.UpdateAsync(id, [NotificationEventType.WantlistSyncCompleted], updated);
		await Assert.That((await _testee.GetForTargetAsync(targetId)).Single().Template).IsEqualTo(updated);
	}

	[Test]
	public async Task DeleteAsync_WhenATargetIsDeleted_CascadesItsSubscriptions()
	{
		var targetId = await CreateTargetAsync();
		await _testee.AddAsync(targetId, [NotificationEventType.WantlistSyncCompleted], NotificationTemplate.None);

		await _targets.DeleteAsync(targetId);

		await Assert.That(await _testee.GetForTargetAsync(targetId)).IsEmpty();
	}
}
