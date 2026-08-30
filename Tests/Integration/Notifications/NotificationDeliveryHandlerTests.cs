using Contracts.DI;
using Contracts.Identity;
using Contracts.Notifications;
using Domain.Identity;
using Domain.Notifications;
using Messaging.Notifications;
using TestInfrastructure;

namespace Integration.Tests.Notifications;

public class NotificationDeliveryHandlerTests
{
	private static readonly UserId User = UserId.From(Guid.NewGuid());

	[Test]
	public async Task Enabled_target_delivering_successfully_sends_once()
	{
		var channel = new RecordingChannel(new DeliverySucceeded());

		await Handle(Guid.NewGuid(), WebhookTarget(enabled: true), channel);

		await Assert.That(channel.Sent).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Transient_result_throws_for_retry()
	{
		var threw = false;
		try
		{
			await Handle(Guid.NewGuid(), WebhookTarget(enabled: true), new RecordingChannel(new DeliveryTransient("boom")));
		}
		catch (NotificationDeliveryFailedException)
		{
			threw = true;
		}

		await Assert.That(threw).IsTrue();
	}

	[Test]
	public async Task Permanent_result_sends_but_does_not_throw()
	{
		var channel = new RecordingChannel(new DeliveryPermanent("401"));

		await Handle(Guid.NewGuid(), WebhookTarget(enabled: true), channel);

		await Assert.That(channel.Sent).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Missing_target_is_skipped_without_a_send()
	{
		var channel = new RecordingChannel(new DeliverySucceeded());

		await Handle(Guid.NewGuid(), target: null, channel);

		await Assert.That(channel.Sent).IsEmpty();
	}

	[Test]
	public async Task Disabled_target_is_skipped_without_a_send()
	{
		var channel = new RecordingChannel(new DeliverySucceeded());

		await Handle(Guid.NewGuid(), WebhookTarget(enabled: false), channel);

		await Assert.That(channel.Sent).IsEmpty();
	}

	private static async Task Handle(Guid targetId, NotificationTarget? target, RecordingChannel channel)
	{
		var targets = INotificationTargetRepository.Mock();
		targets.FindAsync(Any<Guid>(), Any()).Returns(target);

		var index = new FakeKeyedServiceIndex<NotificationChannel, INotificationChannel>().Register(NotificationChannel.Webhook, channel);

		await NotificationDeliveryHandler.Handle(new DeliverNotification(Context(), targetId, User), Scope(), targets.Object, index, CancellationToken.None);
	}

	private static NotificationContext Context() =>
		new(Guid.CreateVersion7(), "watchlist.items.added", DateTimeOffset.UtcNow, "Watchlist items added", "Added 1 item(s).", new NotificationData());

	private static NotificationTarget WebhookTarget(bool enabled) =>
		new(Guid.NewGuid(), "Hook", enabled, new WebhookConfig("https://example.test/hook", "whsec_x"));

	private static IUserScope Scope()
	{
		var scope = IUserScope.Mock();
		scope.AsAsync(Any<UserId>(), Any()).Returns(new NoopDisposable());
		return scope.Object;
	}

	private sealed class RecordingChannel(DeliveryResult result) : INotificationChannel
	{
		public List<NotificationTarget> Sent { get; } = [];

		public NotificationChannel Kind => NotificationChannel.Webhook;

		public Task<DeliveryResult> SendAsync(NotificationContext context, NotificationTarget target, CancellationToken cancellationToken)
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
