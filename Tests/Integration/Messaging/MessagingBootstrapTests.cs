using Contracts.Feed;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Messaging;
using Contracts.Notifications;
using Domain.Feed;
using Domain.Identity;
using Domain.Notifications;
using Messaging;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.DI;
using Shared.Options;
using Wolverine;
using Wolverine.Tracking;

namespace Integration.Tests.Messaging;

[NotInParallel]
public class MessagingBootstrapTests
{
	[Test]
	public async Task Publish_WhenTheMessageIsDurable_IsHandledThroughTheSqliteStore()
	{
		await WithHostAsync(async host =>
		{
			var session = await host.TrackActivity().PublishMessageAndWaitAsync(new Ping("hello"));
			await Assert.That(session.Executed.MessagesOf<Ping>().ToList()).Count().IsEqualTo(1);
		});
	}

	[Test]
	public async Task Publish_WhenTheVariableIsTypedAsTheBaseNotification_StillReachesTheConcreteHandler()
	{
		var owner = UserId.From(Guid.NewGuid());
		BaseNotification notification = new FeedRunCompletedNotification(owner, DateTimeOffset.UtcNow, FeedId.From(Guid.NewGuid()), 3, 1);
		var userScope = DispatchingUserScope();

		await WithHostAsync(
			async host =>
			{
				Func<IMessageContext, Task> publish = async _ =>
				{
					using var scope = host.Services.CreateScope();
					await scope.ServiceProvider.GetRequiredService<IMessagePublisher>().PublishAsync(notification);
				};

				var session = await host.TrackActivity().Timeout(TimeSpan.FromSeconds(30)).ExecuteAndWaitAsync(publish);

				await Assert.That(session.Executed.MessagesOf<FeedRunCompletedNotification>().ToList()).Count().IsEqualTo(1);
				userScope.AsAsync(owner, Any()).WasCalled(Times.Once);
			},
			services => DispatchCollaborators(services, userScope)
		);
	}

	private static Mock<IUserScope> DispatchingUserScope()
	{
		var userScope = IUserScope.Mock();
		userScope.AsAsync(Any<UserId>(), Any()).Returns(new NoopDisposable());
		return userScope;
	}

	private static void DispatchCollaborators(IServiceCollection services, Mock<IUserScope> userScope)
	{
		var subscriptions = INotificationSubscriptionRepository.Mock();
		subscriptions.GetForEventTypeAsync(Any<NotificationEventType>(), Any()).Returns([]);

		services.AddModule<MessagingModule>();
		services.AddSingleton(userScope.Object);
		services.AddSingleton(subscriptions.Object);
		services.AddSingleton(IMediaRepository.Mock().Object);
		services.AddSingleton(IFeedService.Mock().Object);
	}

	private static async Task WithHostAsync(Func<IHost, Task> body, Action<IServiceCollection>? configureServices = null)
	{
		var dbPath = Path.Combine(Path.GetTempPath(), $"wolverine-test-{Guid.NewGuid():N}.db");
		var connectionString = $"Data Source={dbPath}";
		try
		{
			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(
					new Dictionary<string, string?> { [$"{SammlarOptions.SectionName}:{nameof(SammlarOptions.ConnectionString)}"] = connectionString }
				)
				.Build();

			var builder = Host.CreateDefaultBuilder();
			if (configureServices is not null)
			{
				builder.ConfigureServices(configureServices);
			}

			using var host = await builder.UseSammlarrMessaging(configuration).StartAsync();
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

	private sealed class NoopDisposable : IDisposable
	{
		public void Dispose() { }
	}
}
