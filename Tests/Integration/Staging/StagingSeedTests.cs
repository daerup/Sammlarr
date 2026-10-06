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

namespace Integration.Tests.Staging;

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
					Identifier = "2236",
					Strategy = SeedStrategy.Append,
				},
				new SeedFeed
				{
					Name = "Trending shows",
					Provider = FeedProvider.Mdblist,
					Identifier = "88434",
					Strategy = SeedStrategy.Mirror,
					Limit = 10,
				},
			],
			Library = new SeedLibrary { Radarr = [], Sonarr = [] },
			Notifications = new SeedNotifications
			{
				Webhook = new SeedWebhook { Name = "Catcher webhook", Secret = "whsec_staging" },
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
		ArrSinkConfig.Empty with
		{
			Radarr = Instance,
			Sonarr = Instance with { Url = Some.Of("http://sonarr:8989"), RootFolderPath = Some.Of("/tv") },
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
	public async Task Configured_LetsBothConfiguredPeopleSignInWithTheirPasswords()
	{
		await StagingSeed.SeedAsync(_services, StagingScenario.Configured, Settings, CancellationToken.None);

		await using var scope = _services.CreateAsyncScope();
		var signIn = scope.ServiceProvider.GetRequiredService<ISignInService>();
		await Assert.That(await signIn.SignInWithPasswordAsync(Admin, AdminPassword)).IsSuccess();
		await Assert.That(await signIn.SignInWithPasswordAsync(Member, MemberPassword)).IsSuccess();
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
	public async Task Configured_StoresEachPersonsTmdbKey(string username, string password)
	{
		await StagingSeed.SeedAsync(_services, StagingScenario.Configured, Settings, CancellationToken.None);

		await AsAsync(
			username,
			password,
			async services => await Assert.That(await services.GetRequiredService<IApiKeyService>().IsStoredAsync(ApiKeyProvider.Tmdb)).IsTrue()
		);
	}

	[Test]
	public async Task Configured_PointsAWebhookAndPushoverAtTheCatcherAndFollowsTwoListsWithTheMdblistKey()
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
				await Assert.That(feeds.Select(feed => feed.Feed.Enabled)).IsEquivalentTo([true, true]);
			}
		);
	}

	[Test]
	public async Task Configured_WithoutAnMdblistKey_LeavesTheFeedsOff()
	{
		await StagingSeed.SeedAsync(_services, StagingScenario.Configured, Settings with { MdblistKey = new None() }, CancellationToken.None);

		await AsAsync(
			Admin,
			AdminPassword,
			async services =>
			{
				var feeds = (await services.GetRequiredService<IFeedService>().GetAsync()).Feeds;
				await Assert.That(feeds.Select(feed => feed.Feed.Enabled)).IsEquivalentTo([false, false]);
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
		var signedIn = await scope.ServiceProvider.GetRequiredService<ISignInService>().SignInWithPasswordAsync(username, password);
		using var _ = scope.ServiceProvider.GetRequiredService<IUserScope>().As(signedIn.GetOrThrow(error => new InvalidOperationException(error.Reason)));
		await check(scope.ServiceProvider);
	}
}
