using Contracts.Feed;
using Contracts.Media;
using Contracts.Messaging;
using Contracts.Notifications;
using Domain.Announcing;
using Domain.Feed;
using Domain.Identity;
using Domain.Notifications;
using Messaging;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Wolverine;
using Wolverine.Persistence.Durability;
using Wolverine.RDBMS;
using Wolverine.Tracking;

namespace Integration.Tests.Messaging;

[NotInParallel]
public class MessagingBootstrapTests
{
	[Test]
	public async Task Publish_WhenTheMessageIsDurable_IsHandledThroughTheSqliteStore()
	{
		await MessagingHost.WithHostAsync(async host =>
		{
			var session = await host.TrackActivity().PublishMessageAndWaitAsync(new Ping("hello"));
			await Assert.That(session.Executed.MessagesOf<Ping>().ToList()).Count().IsEqualTo(1);
		});
	}

	[Test]
	public async Task Publish_WhenTheVariableIsTypedAsTheINotification_StillReachesTheConcreteHandler()
	{
		var owner = UserId.From(Guid.NewGuid());
		INotification notification = new FeedRunCompletedNotification(owner, DateTimeOffset.UtcNow, FeedId.From(Guid.NewGuid()), 3, 1);

		await MessagingHost.WithHostAsync(
			async host =>
			{
				Func<IMessageContext, Task> publish = async _ =>
				{
					using var scope = host.Services.CreateScope();
					await scope.ServiceProvider.GetRequiredService<IMessagePublisher>().PublishAsync(notification);
				};

				var session = await host.TrackActivity().Timeout(TimeSpan.FromSeconds(30)).ExecuteAndWaitAsync(publish);

				await Assert.That(session.Executed.MessagesOf<FeedRunCompletedNotification>().ToList()).Count().IsEqualTo(1);
			},
			DispatchCollaborators
		);
	}

	[Test]
	public async Task Start_ForTheSqliteStore_OpensConnectionsThatDoNotMemoryMapTheFileSoACompactionCanShrinkIt()
	{
		await MessagingHost.WithHostAsync(async host =>
		{
			var store = (IMessageDatabase)host.Services.GetRequiredService<IMessageStore>();
			await using var connection = await store.DataSource.OpenConnectionAsync();
			await using var command = connection.CreateCommand();
			command.CommandText = "PRAGMA mmap_size";

			await Assert.That(Convert.ToInt64(await command.ExecuteScalarAsync())).IsEqualTo(0);
		});
	}

	private static void DispatchCollaborators(IServiceCollection services)
	{
		var subscriptions = INotificationSubscriptionRepository.Mock();
		subscriptions.GetForEventTypeAsync(Any<NotificationEventType>(), Any()).Returns([]);

		services.AddModule<MessagingModule>();
		services.AddSingleton(MessagingHost.PassingUserScope().Object);
		services.AddSingleton(subscriptions.Object);
		services.AddSingleton(IMediaRepository.Mock().Object);
		services.AddSingleton(IFeedRepository.Mock().Object);
	}
}
