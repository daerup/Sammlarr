using Contracts;
using Contracts.Notifications;
using Domain.Identity;
using Domain.Notifications;
using Integration.Tests.Messaging;
using Messaging.Notifications;
using TestInfrastructure;
using Wolverine;

namespace Integration.Tests.Notifications;

public class NotificationDeliveryHandlerTests
{
	private static readonly UserId User = UserId.From(Guid.NewGuid());

	[Test]
	public async Task Handle_WhenDeliverySucceeds_SendsOnceAndRecordsDelivered()
	{
		var channel = new RecordingChannel(new DeliverySucceeded());

		var outcomes = await Handle(Guid.NewGuid(), WebhookTarget(enabled: true), channel);

		await Assert.That(channel.Sent).Count().IsEqualTo(1);
		await Assert.That(outcomes.Recorded.Single()).IsOfUnionType<DeliveryOutcome, Delivered>();
	}

	[Test]
	public async Task Handle_WhenDeliverySucceedsOnALaterAttempt_StillRecordsDelivered()
	{
		var outcomes = await Handle(Guid.NewGuid(), WebhookTarget(enabled: true), new RecordingChannel(new DeliverySucceeded()), attempts: 3);

		await Assert.That(outcomes.Recorded.Single()).IsOfUnionType<DeliveryOutcome, Delivered>();
	}

	[Test]
	public async Task Handle_WhenTheResultIsRetryable_RecordsRetryingAndThrows()
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
	public async Task Handle_WhenRetryableOnTheFinalAttempt_RecordsFailedWithoutThrowing()
	{
		var channel = new RecordingChannel(new DeliveryRetryable("still down"));

		var outcomes = await Handle(Guid.NewGuid(), WebhookTarget(enabled: true), channel, attempts: NotificationDeliveryRetry.MaxAttempts);

		await Assert.That(outcomes.Recorded.Single()).IsOfUnionType<DeliveryOutcome, Failed>();
	}

	[Test]
	public async Task Handle_WhenTheResultIsUnrecoverable_RecordsFailedWithoutThrowing()
	{
		var channel = new RecordingChannel(new DeliveryUnrecoverable("401"));

		var outcomes = await Handle(Guid.NewGuid(), WebhookTarget(enabled: true), channel);

		await Assert.That(channel.Sent).Count().IsEqualTo(1);
		await Assert.That(outcomes.Recorded.Single()).IsOfUnionType<DeliveryOutcome, Failed>();
	}

	[Test]
	public async Task Handle_WhenTheTargetIsMissing_SkipsWithoutSendingOrRecording()
	{
		var channel = new RecordingChannel(new DeliverySucceeded());

		var outcomes = await Handle(Guid.NewGuid(), target: new None(), channel);

		await Assert.That(channel.Sent).IsEmpty();
		await Assert.That(outcomes.Recorded).IsEmpty();
	}

	[Test]
	public async Task Handle_WhenTheTargetIsDisabled_SkipsWithoutSendingOrRecording()
	{
		var channel = new RecordingChannel(new DeliverySucceeded());

		var outcomes = await Handle(Guid.NewGuid(), WebhookTarget(enabled: false), channel);

		await Assert.That(channel.Sent).IsEmpty();
		await Assert.That(outcomes.Recorded).IsEmpty();
	}

	private static async Task<CapturingOutcomes> Handle(
		Guid targetId,
		Optional<NotificationTarget> target,
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
			MessagingHost.PassingUserScope().Object,
			targets.Object,
			index,
			outcomes,
			clock.Object,
			CancellationToken.None
		);
		return outcomes;
	}

	private static NotificationContext Context() =>
		new(
			Guid.CreateVersion7(),
			NotificationEventType.WantlistSyncCompleted,
			NotificationLevel.Success,
			DateTimeOffset.UtcNow,
			"Wantlist sync completed",
			"Added 1 item(s).",
			new NotificationData()
		);

	private static Some<NotificationTarget> WebhookTarget(bool enabled)
	{
		return Some.Of(new NotificationTarget(Guid.NewGuid(), "Hook", enabled, new WebhookConfig("https://example.test/hook", "whsec_x")));
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

		public Task<DeliveryResult> SendAsync(
			NotificationContext context,
			NotificationTarget target,
			NotificationTemplate template,
			CancellationToken cancellationToken = default
		)
		{
			Sent.Add(target);
			return Task.FromResult(result);
		}
	}
}
