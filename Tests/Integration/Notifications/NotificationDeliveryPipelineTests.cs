using Contracts;
using Contracts.DI;
using Contracts.Messaging;
using Contracts.Notifications;
using Domain.Identity;
using Domain.Notifications;
using Integration.Tests.Messaging;
using Messaging;
using Messaging.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.DI;
using TestInfrastructure;
using Wolverine;
using Wolverine.Runtime;
using Wolverine.Tracking;

namespace Integration.Tests.Notifications;

public class NotificationDeliveryPipelineTests
{
	private static readonly UserId User = UserId.From(Guid.NewGuid());

	[Test]
	public async Task Publish_ThroughTheMessagePublisherSeam_Delivers()
	{
		var channel = new RecordingChannel();
		await WithHostAsync(
			channel,
			async host =>
			{
				Func<IMessageContext, Task> publish = async _ =>
				{
					using var scope = host.Services.CreateScope();
					await scope
						.ServiceProvider.GetRequiredService<IMessagePublisher>()
						.PublishAsync(new WantlistSyncCompletedNotification(User, DateTimeOffset.UtcNow, [], [], [], [], []));
				};

				await host.TrackActivity().Timeout(TimeSpan.FromSeconds(30)).ExecuteAndWaitAsync(publish);

				await Assert.That(channel.Sent).Count().IsEqualTo(1);
			}
		);
	}

	[Test]
	public async Task Serializer_ForATemplateWithAndWithoutItsFields_ReadsTheDeliveryBack()
	{
		var template = new NotificationTemplate(Some.Of("{{ title }}"), new None(), Some.Of("{{ summary }}"), Some.Of(NotificationLevel.Warning), new None());
		var context = new NotificationContext(
			Guid.NewGuid(),
			NotificationEventType.FeedRunFailed,
			NotificationLevel.Error,
			DateTimeOffset.UnixEpoch,
			"Feed run failed",
			"The source could not be read",
			new NotificationData { Reason = "HTTP 404" }
		);
		var message = new DeliverNotification(context, Guid.NewGuid(), User, template);

		await WithHostAsync(
			new RecordingChannel(),
			async host =>
			{
				var serializer = host.Services.GetRequiredService<IWolverineRuntime>().Options.DefaultSerializer;

				var read = (DeliverNotification)serializer.ReadFromData(typeof(DeliverNotification), new Envelope { Data = serializer.WriteMessage(message) });

				await Assert.That(read.Template).IsEqualTo(template);
				await Assert.That(read.Context.Data.Reason).IsEqualTo("HTTP 404");
			}
		);
	}

	private static async Task WithHostAsync(RecordingChannel channel, Func<IHost, Task> body)
	{
		var subscriptions = INotificationSubscriptionRepository.Mock();
		subscriptions
			.GetForEventTypeAsync(Any<NotificationEventType>(), Any())
			.Returns([new NotificationSubscription(Guid.NewGuid(), Guid.NewGuid(), [NotificationEventType.WantlistSyncCompleted], NotificationTemplate.None)]);

		var targets = INotificationTargetRepository.Mock();
		targets
			.FindAsync(Any<Guid>(), Any())
			.Returns(Some.Of(new NotificationTarget(Guid.NewGuid(), "Hook", true, new WebhookConfig("https://example.test/hook", "whsec_x"))));

		var index = new FakeKeyedServiceIndex<NotificationChannel, INotificationChannel>().Register(NotificationChannel.Webhook, channel);

		var clock = IDateTimeProvider.Mock();
		clock.Now.Returns(DateTimeOffset.UnixEpoch);

		await MessagingHost.WithHostAsync(
			body,
			services =>
			{
				services.AddModule<MessagingModule>();
				services.AddSingleton(subscriptions.Object);
				services.AddSingleton(targets.Object);
				services.AddSingleton(MessagingHost.PassingUserScope().Object);
				services.AddSingleton(Contracts.Media.IMediaRepository.Mock().Object);
				services.AddSingleton(Contracts.Feed.IFeedRepository.Mock().Object);
				services.AddSingleton<IKeyedServiceIndex<NotificationChannel, INotificationChannel>>(index);
				services.AddSingleton<INotificationDeliveryOutcomeRepository>(new NoopOutcomes());
				services.AddSingleton(clock.Object);
			}
		);
	}

	private sealed class RecordingChannel : INotificationChannel
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
			return Task.FromResult<DeliveryResult>(new DeliverySucceeded());
		}
	}

	private sealed class NoopOutcomes : INotificationDeliveryOutcomeRepository
	{
		public Task RecordAsync(
			Guid eventId,
			Guid targetId,
			NotificationEventType eventType,
			DeliveryOutcome outcome,
			CancellationToken cancellationToken = default
		) => Task.CompletedTask;

		public Task<IReadOnlyDictionary<Guid, IReadOnlyList<NotificationDeliveryOutcome>>> GetRecentByTargetAsync(
			int count,
			CancellationToken cancellationToken = default
		) =>
			Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<NotificationDeliveryOutcome>>>(
				new Dictionary<Guid, IReadOnlyList<NotificationDeliveryOutcome>>()
			);
	}
}
