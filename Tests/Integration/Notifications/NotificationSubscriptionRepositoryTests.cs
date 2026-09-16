using Contracts.Notifications;
using DB.Data.Notifications;
using Domain.Notifications;
using Harness;
using TestInfrastructure;

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
		await _targets.CreateAsync(new NotificationTarget(Guid.Empty, "Target", true, new PushoverConfig("token", "key")));

	[Test]
	public async Task GetAsync_WhenASubscriptionExists_IsReadableByTargetAndEventType()
	{
		var targetId = await CreateTargetAsync();

		await _testee.AddAsync(targetId, [NotificationEventType.WatchlistSynced], NotificationTemplate.None);

		var byTarget = await _testee.GetForTargetAsync(targetId);
		var byEventType = await _testee.GetForEventTypeAsync(NotificationEventType.WatchlistSynced);

		await Assert.That(byTarget).Count().IsEqualTo(1);
		await Assert.That(byTarget[0].EventTypes).Contains(NotificationEventType.WatchlistSynced);
		await Assert.That(byEventType).Count().IsEqualTo(1);
		await Assert.That(byEventType[0].TargetId).IsEqualTo(targetId);
	}

	[Test]
	public async Task GetByEventTypeAsync_WhenASubscriptionIsBundled_MatchesEachOfItsEvents()
	{
		var targetId = await CreateTargetAsync();

		await _testee.AddAsync(targetId, [NotificationEventType.WatchlistSynced, NotificationEventType.FeedRunCompleted], NotificationTemplate.None);

		await Assert.That(await _testee.GetForEventTypeAsync(NotificationEventType.WatchlistSynced)).Count().IsEqualTo(1);
		await Assert.That(await _testee.GetForEventTypeAsync(NotificationEventType.FeedRunCompleted)).Count().IsEqualTo(1);
		await Assert.That(await _testee.GetForEventTypeAsync(NotificationEventType.FeedRunFailed)).IsEmpty();
	}

	[Test]
	public async Task UpsertAsync_WhenTemplateAndLevelChange_RoundTripAndUpdate()
	{
		var targetId = await CreateTargetAsync();
		var id = await _testee.AddAsync(
			targetId,
			[NotificationEventType.WatchlistSynced],
			new NotificationTemplate(Some.Of("{{ title }}"), new None(), new None(), Some.Of(NotificationLevel.Warning))
		);

		var created = (await _testee.GetForTargetAsync(targetId)).Single();
		await Assert.That(created.Template.Body.Match(value => value, () => null!)).IsEqualTo("{{ title }}");
		await Assert.That(created.Template.Level.Match(value => (NotificationLevel?)value, () => null)).IsEqualTo(NotificationLevel.Warning);

		await _testee.UpdateAsync(
			id,
			[NotificationEventType.WatchlistSynced],
			new NotificationTemplate(Some.Of("{{ summary }}"), new None(), new None(), new None())
		);

		var updated = (await _testee.GetForTargetAsync(targetId)).Single();
		await Assert.That(updated.Template.Body.Match(value => value, () => null!)).IsEqualTo("{{ summary }}");
		await Assert.That(updated.Template.Level.Match(value => (NotificationLevel?)value, () => null)).IsNull();
	}

	[Test]
	public async Task DeleteAsync_WhenATargetIsDeleted_CascadesItsSubscriptions()
	{
		var targetId = await CreateTargetAsync();
		await _testee.AddAsync(targetId, [NotificationEventType.WatchlistSynced], NotificationTemplate.None);

		await _targets.DeleteAsync(targetId);

		await Assert.That(await _testee.GetForTargetAsync(targetId)).IsEmpty();
	}
}
