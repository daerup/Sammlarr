using API;
using Contracts.Auth;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Jobs;
using Contracts.Notifications;
using Contracts.Settings;
using Contracts.Setup;
using Contracts.Wantlist;
using Core;
using DB;
using Domain.Jobs;
using Domain.Media;
using Domain.Notifications;
using Domain.Providers;
using Domain.Wantlist.Arr;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using TestInfrastructure;
using Web.Staging;

namespace Web.Tests.Staging;

public class StagingSeedTests : IAsyncDisposable
{
	private const string Catcher = "http://catcher:8080/session";

	private const string Admin = "ada";

	private const string AdminPassword = "ada-staging";

	private const string Member = "grace";

	private const string MemberPassword = "grace-staging";

	private static readonly ArrInstanceConfig Instance = new()
	{
		Url = Some.Of("http://radarr:7878"),
		ApiKey = Some.Of("0123456789abcdef0123456789abcdef"),
		QualityProfileId = Some.Of(4),
		RootFolderPath = Some.Of("/movies"),
	};

	private static readonly StagingSettings Settings = new(
		new StagingPersonas
		{
			Admin = new StagingPersona { Name = Admin, Password = AdminPassword },
			Member = new StagingPersona { Name = Member, Password = MemberPassword },
		},
		new StagingSeedData
		{
			Wantlist =
			[
				new SeedTitle { Type = MediaType.Movie, Tmdb = "687163" },
				new SeedTitle
				{
					Type = MediaType.Show,
					Tmdb = "1396",
					Tvdb = "81189",
				},
			],
			Feeds =
			[
				new SeedFeed
				{
					Name = "Top movies of the week",
					Provider = FeedProvider.Mdblist,
					Identifier = "garycrawfordgc/top-movies-of-the-week",
					Strategy = SeedStrategy.Append,
				},
				new SeedFeed
				{
					Name = "Trending shows",
					Provider = FeedProvider.Mdblist,
					Identifier = "snoak/trakt-s-trending-shows",
					Strategy = SeedStrategy.Mirror,
					Limit = 10,
				},
				Feed("Marvel movies", FeedProvider.Tmdb, "1"),
				Feed("This season on AniList", FeedProvider.AniList, "search/anime/this-season"),
				Feed("This season on MyAnimeList", FeedProvider.Mal, "anime/season"),
				Feed("Trending on Trakt", FeedProvider.Trakt, "movies/trending"),
			],
			Library = new SeedLibrary { Radarr = [], Sonarr = [] },
			Notifications = new SeedNotifications
			{
				Template = new SeedTemplate { Title = "{{ title }}", Message = "{{ summary }}" },
				Webhook = new SeedWebhook { Name = "Catcher webhook", Secret = "whsec_c2FtbWxhcnItc3RhZ2luZy13ZWJob29rLXNlY3JldCE=" },
				Pushover = new SeedPushover
				{
					Name = "Catcher Pushover",
					AppToken = "staging-app-token",
					UserKey = "staging-user-key",
				},
			},
		},
		"staging-tmdb-key",
		Some.Of("staging-mdblist-key"),
		Some.Of("staging-mal-client-id"),
		Some.Of("staging-trakt-client-id"),
		ArrSinkConfig.Empty with
		{
			Radarr = Instance,
			Sonarr = Instance with { Url = Some.Of("http://sonarr:8989"), RootFolderPath = Some.Of("/tv"), AnimeRootFolderPath = Some.Of("/anime") },
		},
		Catcher
	);

	private readonly string _directory = Directory.CreateTempSubdirectory("sammlarr-staging-").FullName;

	private readonly ServiceProvider _services;

	public StagingSeedTests()
	{
		var configuration = new ConfigurationBuilder()
			.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
			.AddInMemoryCollection(
				new Dictionary<string, string?>
				{
					["Sammlarr:ConnectionString"] = $"Data Source={Path.Combine(_directory, "sammlarr.db")}",
					["Sammlarr:Accounts:SetupCode"] = "SAMM-LARR",
					["Sammlarr:Accounts:MinimumPasswordLength"] = "5",
				}
			)
			.Build();
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<IConfiguration>(configuration);
		services.AddModule<SharedModule>();
		services.AddModule<DbModule>();
		services.AddModule<CoreModule>();
		services.AddModule<ApiModule>();
		services.AddStagingSeed(configuration);
		services.AddDataProtection().UseEphemeralDataProtectionProvider();
		_services = services.BuildServiceProvider();
	}

	public async ValueTask DisposeAsync()
	{
		await _services.DisposeAsync();
		SqliteConnection.ClearAllPools();
		Directory.Delete(_directory, recursive: true);
	}

	[Test]
	public async Task Configured_FinishesSetupWithTheAdminsWantlistOnRadarrAndSonarrAndASyncQueued()
	{
		await StagingSeed.SeedAsync(_services, StagingScenario.Configured, Settings, CancellationToken.None);

		await AsAsync(
			Admin,
			AdminPassword,
			async services =>
			{
				await Assert.That((await services.GetRequiredService<IProviderConfigService>().GetAsync()).Sink).IsEqualTo(SinkKind.Arr);
				var arr = await services.GetRequiredService<IArrSinkConfigService>().GetAsync();
				await Assert.That(arr.Radarr.Url).IsSome().And.IsEqualTo("http://radarr:7878");
				await Assert.That(arr.Sonarr.Url).IsSome().And.IsEqualTo("http://sonarr:8989");
				await Assert.That(arr.Sonarr.AnimeRootFolderPath).IsSome().And.IsEqualTo("/anime");
				await Assert.That((await services.GetRequiredService<IWantlistService>().GetAsync()).Ids).Count().IsEqualTo(2);
				var queue = await services.GetRequiredService<IJobQueueView>().GetAsync();
				await Assert.That(queue.Jobs.Count(job => job.Request is SyncWantlist)).IsEqualTo(1);
				await Assert.That(await services.GetRequiredService<IOnboardingService>().IsCompleteAsync()).IsTrue();
			}
		);
	}

	[Test]
	[Arguments(Admin, AdminPassword)]
	[Arguments(Member, MemberPassword)]
	public async Task Configured_LetsEachPersonSignInWithTheirPasswordAndStoresTheirKeys(string username, string password)
	{
		await StagingSeed.SeedAsync(_services, StagingScenario.Configured, Settings, CancellationToken.None);

		await AsAsync(
			username,
			password,
			async services =>
			{
				var keys = services.GetRequiredService<IApiKeyService>();
				foreach (var slot in new[] { ApiKeyProvider.Tmdb, ApiKeyProvider.Mdblist, ApiKeyProvider.Mal, ApiKeyProvider.Trakt })
				{
					await Assert.That(await keys.IsStoredAsync(slot)).IsTrue();
				}
			}
		);
	}

	[Test]
	public async Task Configured_PointsAWebhookAndPushoverAtTheCatcherAndFollowsAListOnEveryFeedProviderWithEveryKey()
	{
		await StagingSeed.SeedAsync(_services, StagingScenario.Configured, Settings, CancellationToken.None);

		await AsAsync(
			Admin,
			AdminPassword,
			async services =>
			{
				var targets = await services.GetRequiredService<INotificationTargetRepository>().GetAllAsync();
				await Assert.That(targets.Count(target => target.Config is WebhookConfig { Url: $"{Catcher}/webhook" })).IsEqualTo(1);
				await Assert.That(targets.Count(target => target.Config is PushoverConfig)).IsEqualTo(1);
				var feeds = (await services.GetRequiredService<IFeedService>().GetAsync()).Feeds;
				await Assert.That(feeds.Select(feed => feed.Feed.Provider).Distinct()).IsEquivalentTo(Enum.GetValues<FeedProvider>());
				await Assert.That(feeds.Select(feed => feed.Feed.Enabled)).IsEquivalentTo([true, true, true, true, true, true]);
			}
		);
	}

	[Test]
	public async Task Configured_SubscribesBothCatcherTargetsToEveryEventWithTheSeededTemplate()
	{
		await StagingSeed.SeedAsync(_services, StagingScenario.Configured, Settings, CancellationToken.None);

		await AsAsync(
			Admin,
			AdminPassword,
			async services =>
			{
				var subscriptions = services.GetRequiredService<INotificationSubscriptionRepository>();
				var targets = await services.GetRequiredService<INotificationTargetRepository>().GetAllAsync();
				foreach (var target in targets.Where(target => target.Config is WebhookConfig or PushoverConfig))
				{
					var subscription = (await subscriptions.GetForTargetAsync(target.Id)).Single();
					await Assert.That(subscription.EventTypes).IsEquivalentTo(Enum.GetValues<NotificationEventType>());
					await Assert.That(subscription.Template.Message).IsSome().And.IsEqualTo("{{ summary }}");
				}
			}
		);
	}

	[Test]
	public async Task Configured_WithoutTheMdblistMalAndTraktKeys_LeavesOnlyTheirFeedsOff()
	{
		await StagingSeed.SeedAsync(
			_services,
			StagingScenario.Configured,
			Settings with
			{
				MdblistKey = new None(),
				MalKey = new None(),
				TraktKey = new None(),
			},
			CancellationToken.None
		);

		await AsAsync(
			Admin,
			AdminPassword,
			async services =>
			{
				var feeds = (await services.GetRequiredService<IFeedService>().GetAsync()).Feeds;
				await Assert.That(feeds.Where(feed => feed.Feed.Enabled).Select(feed => feed.Feed.Provider)).IsEquivalentTo([FeedProvider.Tmdb, FeedProvider.AniList]);
			}
		);
	}

	[Test]
	public async Task Fresh_LeavesTheInstallWaitingForItsAdminBehindTheConfiguredSetupCode()
	{
		await StagingSeed.SeedAsync(_services, StagingScenario.Fresh, Settings, CancellationToken.None);

		await using var scope = _services.CreateAsyncScope();
		var onboarding = scope.ServiceProvider.GetRequiredService<IOnboardingService>();
		await Assert.That(await onboarding.NeedsAdminAsync()).IsTrue();
		await Assert.That(await onboarding.CodeProblemAsync("samm-larr")).IsNone();
	}

	private async Task AsAsync(string username, string password, Func<IServiceProvider, Task> check)
	{
		await using var scope = _services.CreateAsyncScope();
		var signedIn = await Assert.That(await scope.ServiceProvider.GetRequiredService<ISignInService>().SignInWithPasswordAsync(username, password)).IsSuccess();
		using var _ = scope.ServiceProvider.GetRequiredService<IUserScope>().As(signedIn!);
		await check(scope.ServiceProvider);
	}

	private static SeedFeed Feed(string name, FeedProvider provider, string identifier)
	{
		return new SeedFeed
		{
			Name = name,
			Provider = provider,
			Identifier = identifier,
			Strategy = SeedStrategy.Append,
			Limit = 10,
		};
	}
}
