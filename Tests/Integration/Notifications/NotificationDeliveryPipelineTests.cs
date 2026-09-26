using Contracts;
using Contracts.DI;
using Contracts.Identity;
using Contracts.Messaging;
using Contracts.Notifications;
using Domain.Identity;
using Domain.Notifications;
using Domain.Notifications.Events;
using Messaging;
using Messaging.Notifications;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.DI;
using Shared.Options;
using TestInfrastructure;
using Wolverine;
using Wolverine.Tracking;

namespace Integration.Tests.Notifications;

public class NotificationDeliveryPipelineTests
{
	private static readonly UserId User = UserId.From(Guid.NewGuid());

	[Test]
	public async Task Publish_WhenAnEventIsRaised_DeliversToEachSubscribedTarget()
	{
		var channel = new RecordingChannel();
		await WithHostAsync(
			channel,
			async host =>
			{
				var session = await host.TrackActivity()
					.Timeout(TimeSpan.FromSeconds(30))
					.PublishMessageAndWaitAsync(new WatchlistSyncedEvent(User, DateTimeOffset.UtcNow, [], [], [], [], []));

				await Assert.That(session.Executed.MessagesOf<DeliverNotification>().ToList()).Count().IsEqualTo(1);
				await Assert.That(channel.Sent).Count().IsEqualTo(1);
			}
		);
	}

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
						.PublishAsync(new WatchlistSyncedEvent(User, DateTimeOffset.UtcNow, [], [], [], [], []));
				};

				await host.TrackActivity().Timeout(TimeSpan.FromSeconds(30)).ExecuteAndWaitAsync(publish);

				await Assert.That(channel.Sent).Count().IsEqualTo(1);
			}
		);
	}

	private static async Task WithHostAsync(RecordingChannel channel, Func<IHost, Task> body)
	{
		var subscriptions = INotificationSubscriptionRepository.Mock();
		subscriptions
			.GetForEventTypeAsync(Any<NotificationEventType>(), Any())
			.Returns(
				(IReadOnlyList<NotificationSubscription>)
					[new NotificationSubscription(Guid.NewGuid(), Guid.NewGuid(), [NotificationEventType.WatchlistSynced], NotificationTemplate.None)]
			);

		var targets = INotificationTargetRepository.Mock();
		targets
			.FindAsync(Any<Guid>(), Any())
			.Returns(new NotificationTarget(Guid.NewGuid(), "Hook", true, new WebhookConfig("https://example.test/hook", "whsec_x")));

		var userScope = IUserScope.Mock();
		userScope.AsAsync(Any<UserId>(), Any()).Returns(new NoopDisposable());

		var index = new FakeKeyedServiceIndex<NotificationChannel, INotificationChannel>().Register(NotificationChannel.Webhook, channel);

		var clock = IDateTimeProvider.Mock();
		clock.Now.Returns(DateTimeOffset.UnixEpoch);

		var dbPath = Path.Combine(Path.GetTempPath(), $"wolverine-dispatch-test-{Guid.NewGuid():N}.db");
		var connectionString = $"Data Source={dbPath}";
		try
		{
			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(
					new Dictionary<string, string?> { [$"{SammlarOptions.SectionName}:{nameof(SammlarOptions.ConnectionString)}"] = connectionString }
				)
				.Build();

			using var host = await Host.CreateDefaultBuilder()
				.ConfigureServices(services =>
				{
					services.AddModule<MessagingModule>();
					services.AddSingleton(subscriptions.Object);
					services.AddSingleton(targets.Object);
					services.AddSingleton(userScope.Object);
					services.AddSingleton(Contracts.Media.IMediaRepository.Mock().Object);
					services.AddSingleton(Contracts.Feed.IFeedService.Mock().Object);
					services.AddSingleton<IKeyedServiceIndex<NotificationChannel, INotificationChannel>>(index);
					services.AddSingleton<INotificationDeliveryOutcomeRepository>(new NoopOutcomes());
					services.AddSingleton(clock.Object);
				})
				.UseSammlarrMessaging(configuration)
				.StartAsync();

			await body(host);
		}
		finally
		{
			SqliteConnection.ClearAllPools();
			if (File.Exists(dbPath))
			{
				File.Delete(dbPath);
			}
		}
	}

	private sealed class RecordingChannel : INotificationChannel
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

	private sealed class NoopDisposable : IDisposable
	{
		public void Dispose() { }
	}
}
