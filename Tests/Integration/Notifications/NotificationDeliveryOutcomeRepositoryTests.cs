using Contracts.Notifications;
using DB.Data.Notifications;
using Domain.Identity;
using Domain.Notifications;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Notifications;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class NotificationDeliveryOutcomeRepositoryTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	private INotificationTargetRepository _targets = null!;
	private INotificationDeliveryOutcomeRepository _testee = null!;

	[Before(Test)]
	public void Setup()
	{
		_targets = new NotificationTargetRepository(fixture.ContextFactory);
		_testee = new NotificationDeliveryOutcomeRepository(fixture.ContextFactory);
	}

	private Task<Guid> CreateTargetAsync() => _targets.CreateAsync(new NotificationTarget(Guid.Empty, "Target", true, new PushoverConfig("token", "key")));

	private async Task<IReadOnlyList<NotificationDeliveryOutcome>> RecentAsync(Guid targetId, int count = 10) =>
		(await _testee.GetRecentByTargetAsync(count)).GetValueOrDefault(targetId, []);

	[Test]
	public async Task RecordAsync_WhenDelivered_RoundTripsTheOutcome()
	{
		var targetId = await CreateTargetAsync();

		await _testee.RecordAsync(Guid.CreateVersion7(), targetId, NotificationEventType.WantlistSyncCompleted, new Delivered());

		var recent = await RecentAsync(targetId);
		await Assert.That(recent).Count().IsEqualTo(1);
		await Assert.That(recent[0].Outcome).IsOfUnionType<DeliveryOutcome, Delivered>();
	}

	[Test]
	public async Task RecordAsync_WhenRetrying_RoundTripsReasonAndNextAttempt()
	{
		var targetId = await CreateTargetAsync();
		var next = DateTimeOffset.UnixEpoch.AddMinutes(5);

		await _testee.RecordAsync(Guid.CreateVersion7(), targetId, NotificationEventType.WantlistSyncCompleted, new Retrying("HTTP 500", next));

		var recent = await RecentAsync(targetId);
		await Assert
			.That(recent[0].Outcome)
			.IsOfUnionType<DeliveryOutcome, Retrying>()
			.And.Multiple(async retrying =>
			{
				await Assert.That(retrying.Reason).IsEqualTo("HTTP 500");
				await Assert.That(retrying.NextAttemptAt).IsEqualTo(next);
			});
	}

	[Test]
	public async Task RecordAsync_WhenARetryIsFollowedBySuccess_CollapsesToDelivered()
	{
		var targetId = await CreateTargetAsync();
		var eventId = Guid.CreateVersion7();

		await _testee.RecordAsync(eventId, targetId, NotificationEventType.WantlistSyncCompleted, new Retrying("down", DateTimeOffset.UnixEpoch.AddMinutes(1)));
		await _testee.RecordAsync(eventId, targetId, NotificationEventType.WantlistSyncCompleted, new Delivered());

		var recent = await RecentAsync(targetId);
		await Assert.That(recent).Count().IsEqualTo(1);
		await Assert.That(recent[0].Outcome).IsOfUnionType<DeliveryOutcome, Delivered>();
	}

	[Test]
	public async Task GetRecentAsync_WhenMoreExistThanRequested_IsCappedToTheCount()
	{
		var targetId = await CreateTargetAsync();
		for (var i = 0; i < 5; i++)
		{
			await _testee.RecordAsync(Guid.CreateVersion7(), targetId, NotificationEventType.WantlistSyncCompleted, new Failed($"fail {i}"));
		}

		await Assert.That(await RecentAsync(targetId, count: 3)).Count().IsEqualTo(3);
	}

	[Test]
	public async Task RecordAsync_WhenOutcomesAccumulate_PrunesToTheNewestTwentyPerTarget()
	{
		var targetId = await CreateTargetAsync();
		for (var i = 0; i < 25; i++)
		{
			await _testee.RecordAsync(Guid.CreateVersion7(), targetId, NotificationEventType.WantlistSyncCompleted, new Delivered());
		}

		await Assert.That(await RecentAsync(targetId, count: 100)).Count().IsEqualTo(20);
	}

	[Test]
	public async Task Outcomes_WhenTwoUsersExist_AreIsolatedPerUser()
	{
		var targetId = await CreateTargetAsync();
		await _testee.RecordAsync(Guid.CreateVersion7(), targetId, NotificationEventType.WantlistSyncCompleted, new Delivered());

		fixture.CurrentUser.Id = UserId.From(Guid.NewGuid());
		await Assert.That(await RecentAsync(targetId)).IsEmpty();

		fixture.CurrentUser.Id = TestCurrentUser.DefaultId;
		await Assert.That(await RecentAsync(targetId)).Count().IsEqualTo(1);
	}

	[Test]
	public async Task DeleteAsync_WhenATargetIsDeleted_CascadesItsOutcomes()
	{
		var targetId = await CreateTargetAsync();
		await _testee.RecordAsync(Guid.CreateVersion7(), targetId, NotificationEventType.WantlistSyncCompleted, new Delivered());

		await _targets.DeleteAsync(targetId);

		await Assert.That(await RecentAsync(targetId)).IsEmpty();
	}
}
