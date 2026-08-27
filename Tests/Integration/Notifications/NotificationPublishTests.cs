using Contracts.Messaging;
using Domain.Identity;
using Domain.Media;
using Domain.Notifications.Events;
using Messaging;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.DI;
using Shared.Options;
using Wolverine;
using Wolverine.Tracking;

namespace Integration.Tests.Notifications;

public class NotificationPublishTests
{
	[Test]
	public async Task Every_wired_event_is_handled_through_the_durable_sqlite_store()
	{
		await WithHostAsync(async host =>
		{
			var user = UserId.New();
			var occurredAt = DateTimeOffset.UtcNow;
			var synced = new WatchlistSyncedEvent(user, occurredAt, PulledAdds: 1, PulledRemoves: 0, PushedAdds: 1, PushedRemoves: 0);
			var added = new WatchlistItemsAddedEvent(user, occurredAt, [MediaId.From(Guid.NewGuid())]);
			var removed = new WatchlistItemsRemovedEvent(user, occurredAt, [MediaId.From(Guid.NewGuid())]);
			var ping = new TestPingEvent(user, occurredAt, Guid.NewGuid());

			Func<IMessageContext, Task> publishAll = async context =>
			{
				await context.PublishAsync(synced);
				await context.PublishAsync(added);
				await context.PublishAsync(removed);
				await context.PublishAsync(ping);
			};

			var session = await host.TrackActivity().Timeout(TimeSpan.FromSeconds(30)).ExecuteAndWaitAsync(publishAll);

			await Assert.That(session.Executed.MessagesOf<WatchlistSyncedEvent>().ToList()).Count().IsEqualTo(1);
			await Assert.That(session.Executed.MessagesOf<WatchlistItemsAddedEvent>().ToList()).Count().IsEqualTo(1);
			await Assert.That(session.Executed.MessagesOf<WatchlistItemsRemovedEvent>().ToList()).Count().IsEqualTo(1);
			await Assert.That(session.Executed.MessagesOf<TestPingEvent>().ToList()).Count().IsEqualTo(1);
			await Assert.That(session.Executed.SingleMessage<WatchlistSyncedEvent>().Id).IsEqualTo(synced.Id);
		});
	}

	[Test]
	public async Task Publishing_through_the_message_publisher_seam_reaches_the_handler()
	{
		await WithHostAsync(
			async host =>
			{
				var @event = new WatchlistItemsAddedEvent(UserId.New(), DateTimeOffset.UtcNow, [MediaId.From(Guid.NewGuid())]);

				Func<IMessageContext, Task> publish = async _ =>
				{
					using var scope = host.Services.CreateScope();
					await scope.ServiceProvider.GetRequiredService<IMessagePublisher>().PublishAsync(@event);
				};

				var session = await host.TrackActivity().Timeout(TimeSpan.FromSeconds(30)).ExecuteAndWaitAsync(publish);

				await Assert.That(session.Executed.MessagesOf<WatchlistItemsAddedEvent>().ToList()).Count().IsEqualTo(1);
			},
			services => services.AddModule<MessagingModule>()
		);
	}

	private static async Task WithHostAsync(Func<IHost, Task> body, Action<IServiceCollection>? configureServices = null)
	{
		var dbPath = Path.Combine(Path.GetTempPath(), $"wolverine-notifications-test-{Guid.NewGuid():N}.db");
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
}
