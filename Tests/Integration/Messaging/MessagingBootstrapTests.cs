using Contracts.DI;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Domain.Watchlist.Outbox;
using Messaging;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Options;
using TestInfrastructure;
using Wolverine.Tracking;

namespace Integration.Tests.Messaging;

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
	public async Task Handle_ForAWatchlistPush_RunsThroughTheWolverineRuntime()
	{
		await WithHostAsync(
			async host =>
			{
				var operation = new WatchlistPushRequest(UserId.From(Guid.NewGuid()), MediaId.From(Guid.NewGuid()));
				var session = await host.TrackActivity().PublishMessageAndWaitAsync(operation);
				await Assert.That(session.Executed.MessagesOf<WatchlistPushRequest>().ToList()).Count().IsEqualTo(1);
			},
			RegisterHandlerDependencies
		);
	}

	private static void RegisterHandlerDependencies(IServiceCollection services)
	{
		var userScope = IUserScope.Mock();
		userScope.AsAsync(Any<UserId>(), Any()).Returns(new NoopDisposable());
		services.AddSingleton(userScope.Object);

		var outbox = IWatchlistOutbox.Mock();
		outbox.FindPendingAsync(Any<MediaId>(), Any()).Returns((WatchlistOutboxEntry?)null);
		services.AddSingleton(outbox.Object);

		services.AddSingleton(IProviderConfigRepository.Mock().Object);
		services.AddSingleton(IMediaRepository.Mock().Object);
		services.AddSingleton<IKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>>(new FakeKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>());
		services.AddSingleton(IWatchlistOutboxEventFeed.Mock().Object);
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
