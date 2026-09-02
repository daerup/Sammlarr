using Contracts.Notifications;
using DB.Data.Notifications;
using Domain.Identity;
using Domain.Notifications;
using TestInfrastructure;

namespace DB.Tests.Integration.Repositories;

[ClassDataSource<SQLiteFixture>(Shared = SharedType.None)]
public class NotificationDeliveryOutcomeRepositoryTests(SQLiteFixture fixture)
{
	private INotificationTargetRepository _targets = null!;
	private INotificationDeliveryOutcomeRepository _testee = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

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
	public async Task Records_and_reads_back_a_delivered_outcome()
	{
		var targetId = await CreateTargetAsync();

		await _testee.RecordAsync(Guid.CreateVersion7(), targetId, "watchlist.synced", new Delivered());

		var recent = await RecentAsync(targetId);
		await Assert.That(recent).Count().IsEqualTo(1);
		await Assert.That(recent[0].Outcome).IsOfUnionType<DeliveryOutcome, Delivered>();
	}

	[Test]
	public async Task Retrying_outcome_round_trips_reason_and_next_attempt()
	{
		var targetId = await CreateTargetAsync();
		var next = DateTimeOffset.UnixEpoch.AddMinutes(5);

		await _testee.RecordAsync(Guid.CreateVersion7(), targetId, "watchlist.synced", new Retrying("HTTP 500", next));

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
	public async Task Same_event_and_target_upserts_so_a_retry_then_success_collapses_to_delivered()
	{
		var targetId = await CreateTargetAsync();
		var eventId = Guid.CreateVersion7();

		await _testee.RecordAsync(eventId, targetId, "watchlist.synced", new Retrying("down", DateTimeOffset.UnixEpoch.AddMinutes(1)));
		await _testee.RecordAsync(eventId, targetId, "watchlist.synced", new Delivered());

		var recent = await RecentAsync(targetId);
		await Assert.That(recent).Count().IsEqualTo(1);
		await Assert.That(recent[0].Outcome).IsOfUnionType<DeliveryOutcome, Delivered>();
	}

	[Test]
	public async Task Get_recent_is_capped_to_the_requested_count()
	{
		var targetId = await CreateTargetAsync();
		for (var i = 0; i < 5; i++)
		{
			await _testee.RecordAsync(Guid.CreateVersion7(), targetId, "watchlist.synced", new Failed($"fail {i}"));
		}

		await Assert.That(await RecentAsync(targetId, count: 3)).Count().IsEqualTo(3);
	}

	[Test]
	public async Task Outcomes_are_pruned_to_the_newest_twenty_per_target()
	{
		var targetId = await CreateTargetAsync();
		for (var i = 0; i < 25; i++)
		{
			await _testee.RecordAsync(Guid.CreateVersion7(), targetId, "watchlist.synced", new Delivered());
		}

		await Assert.That(await RecentAsync(targetId, count: 100)).Count().IsEqualTo(20);
	}

	[Test]
	public async Task Outcomes_are_isolated_per_user()
	{
		var targetId = await CreateTargetAsync();
		await _testee.RecordAsync(Guid.CreateVersion7(), targetId, "watchlist.synced", new Delivered());

		fixture.CurrentUser.Id = UserId.From(Guid.NewGuid());
		await Assert.That(await RecentAsync(targetId)).IsEmpty();

		fixture.CurrentUser.Id = TestCurrentUser.DefaultId;
		await Assert.That(await RecentAsync(targetId)).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Deleting_a_target_cascades_its_outcomes()
	{
		var targetId = await CreateTargetAsync();
		await _testee.RecordAsync(Guid.CreateVersion7(), targetId, "watchlist.synced", new Delivered());

		await _targets.DeleteAsync(targetId);

		await Assert.That(await RecentAsync(targetId)).IsEmpty();
	}
}
