using Contracts;
using Contracts.DI;
using Contracts.Identity;
using Contracts.Notifications;
using Domain.Identity;
using Domain.Notifications;
using Messaging.Notifications;
using TestInfrastructure;
using Wolverine;

namespace Integration.Tests.Notifications;

public class NotificationDeliveryHandlerTests
{
	private static readonly UserId User = UserId.From(Guid.NewGuid());

	[Test]
	public async Task Successful_delivery_sends_once_and_records_delivered()
	{
		var channel = new RecordingChannel(new DeliverySucceeded());

		var outcomes = await Handle(Guid.NewGuid(), WebhookTarget(enabled: true), channel);

		await Assert.That(channel.Sent).Count().IsEqualTo(1);
		await Assert.That(outcomes.Recorded.Single()).IsOfUnionType<DeliveryOutcome, Delivered>();
	}

	[Test]
	public async Task Successful_delivery_on_a_later_attempt_still_records_delivered()
	{
		var outcomes = await Handle(Guid.NewGuid(), WebhookTarget(enabled: true), new RecordingChannel(new DeliverySucceeded()), attempts: 3);

		await Assert.That(outcomes.Recorded.Single()).IsOfUnionType<DeliveryOutcome, Delivered>();
	}

	[Test]
	public async Task Retryable_result_records_retrying_and_throws()
	{
		var outcomes = new CapturingOutcomes();
		var threw = false;
		try
		{
			await Handle(Guid.NewGuid(), WebhookTarget(enabled: true), new RecordingChannel(new DeliveryRetryable("boom")), outcomes: outcomes);
		}
		catch (NotificationDeliveryFailedException)
		{
			threw = true;
		}

		await Assert.That(threw).IsTrue();
		await Assert.That(outcomes.Recorded.Single()).IsOfUnionType<DeliveryOutcome, Retrying>();
	}

	[Test]
	public async Task Retryable_on_the_final_attempt_records_failed_and_does_not_throw()
	{
		var channel = new RecordingChannel(new DeliveryRetryable("still down"));

		var outcomes = await Handle(Guid.NewGuid(), WebhookTarget(enabled: true), channel, attempts: NotificationDeliveryRetry.MaxAttempts);

		await Assert.That(outcomes.Recorded.Single()).IsOfUnionType<DeliveryOutcome, Failed>();
	}

	[Test]
	public async Task Unrecoverable_result_records_failed_without_throwing()
	{
		var channel = new RecordingChannel(new DeliveryUnrecoverable("401"));

		var outcomes = await Handle(Guid.NewGuid(), WebhookTarget(enabled: true), channel);

		await Assert.That(channel.Sent).Count().IsEqualTo(1);
		await Assert.That(outcomes.Recorded.Single()).IsOfUnionType<DeliveryOutcome, Failed>();
	}

	[Test]
	public async Task Missing_target_is_skipped_without_a_send_or_record()
	{
		var channel = new RecordingChannel(new DeliverySucceeded());

		var outcomes = await Handle(Guid.NewGuid(), target: null, channel);

		await Assert.That(channel.Sent).IsEmpty();
		await Assert.That(outcomes.Recorded).IsEmpty();
	}

	[Test]
	public async Task Disabled_target_is_skipped_without_a_send_or_record()
	{
		var channel = new RecordingChannel(new DeliverySucceeded());

		var outcomes = await Handle(Guid.NewGuid(), WebhookTarget(enabled: false), channel);

		await Assert.That(channel.Sent).IsEmpty();
		await Assert.That(outcomes.Recorded).IsEmpty();
	}

	private static async Task<CapturingOutcomes> Handle(
		Guid targetId,
		NotificationTarget? target,
		RecordingChannel channel,
		int attempts = 1,
		CapturingOutcomes? outcomes = null
	)
	{
		var targets = INotificationTargetRepository.Mock();
		targets.FindAsync(Any<Guid>(), Any()).Returns(target);

		var index = new FakeKeyedServiceIndex<NotificationChannel, INotificationChannel>().Register(NotificationChannel.Webhook, channel);
		outcomes ??= new CapturingOutcomes();

		var clock = IDateTimeProvider.Mock();
		clock.Now.Returns(DateTimeOffset.UnixEpoch);

		await NotificationDeliveryHandler.Handle(
			new DeliverNotification(Context(), targetId, User, NotificationTemplate.None),
			new Envelope { Attempts = attempts },
			Scope(),
			targets.Object,
			index,
			outcomes,
			clock.Object,
			CancellationToken.None
		);
		return outcomes;
	}

	private static NotificationContext Context() =>
		new(Guid.CreateVersion7(), NotificationEventType.WatchlistSynced, DateTimeOffset.UtcNow, "Watchlist synced", "Added 1 item(s).", new NotificationData());

	private static NotificationTarget WebhookTarget(bool enabled) =>
		new(Guid.NewGuid(), "Hook", enabled, new WebhookConfig("https://example.test/hook", "whsec_x"));

	private static IUserScope Scope()
	{
		var scope = IUserScope.Mock();
		scope.AsAsync(Any<UserId>(), Any()).Returns(new NoopDisposable());
		return scope.Object;
	}

	private sealed class CapturingOutcomes : INotificationDeliveryOutcomeRepository
	{
		public List<DeliveryOutcome> Recorded { get; } = [];

		public Task RecordAsync(
			Guid eventId,
			Guid targetId,
			NotificationEventType eventType,
			DeliveryOutcome outcome,
			CancellationToken cancellationToken = default
		)
		{
			Recorded.Add(outcome);
			return Task.CompletedTask;
		}

		public Task<IReadOnlyDictionary<Guid, IReadOnlyList<NotificationDeliveryOutcome>>> GetRecentByTargetAsync(
			int count,
			CancellationToken cancellationToken = default
		) =>
			Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<NotificationDeliveryOutcome>>>(
				new Dictionary<Guid, IReadOnlyList<NotificationDeliveryOutcome>>()
			);
	}

	private sealed class RecordingChannel(DeliveryResult result) : INotificationChannel
	{
		public List<NotificationTarget> Sent { get; } = [];

		public NotificationChannel Kind => NotificationChannel.Webhook;

		public Task<DeliveryResult> SendAsync(
			NotificationContext context,
			NotificationTarget target,
			NotificationTemplate template,
			CancellationToken cancellationToken
		)
		{
			Sent.Add(target);
			return Task.FromResult(result);
		}
	}

	private sealed class NoopDisposable : IDisposable
	{
		public void Dispose() { }
	}
}
