using Contracts.Auth;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Jobs;
using Contracts.Media;
using Contracts.Notifications;
using Contracts.Providers;
using Contracts.Settings;
using Contracts.Setup;
using Contracts.Wantlist;
using DB;
using Domain.Auth;
using Domain.Feed;
using Domain.Jobs;
using Domain.Notifications;
using Domain.Processing;
using Domain.Providers;
using Domain.Wantlist;
using Domain.Wantlist.Arr;
using Domain.Wantlist.Sync;
using Shared.Options;

namespace Web.Staging;

internal enum StagingScenario
{
	Configured,
	Fresh,
}

internal sealed record StagingSettings(
	StagingPersonas Personas,
	StagingSeedData Seed,
	string TmdbKey,
	Optional<string> MdblistKey,
	Optional<string> MalKey,
	Optional<string> TraktKey,
	ArrSinkConfig Arr,
	string CatcherUrl
);

internal static class StagingSeed
{
	internal static async Task SeedAsync(IServiceProvider services, StagingScenario scenario, StagingSettings settings, CancellationToken cancellationToken)
	{
		await services.MigrateDatabaseAsync(cancellationToken);
		await using var scope = services.CreateAsyncScope();
		var scoped = scope.ServiceProvider;
		await (
			scenario switch
			{
				StagingScenario.Configured => ConfigureAsync(scoped, settings, cancellationToken),
				StagingScenario.Fresh => Task.CompletedTask,
			}
		);
	}

	private static async Task ConfigureAsync(IServiceProvider scoped, StagingSettings settings, CancellationToken cancellationToken)
	{
		var users = scoped.GetRequiredService<IUserScope>();
		var personas = settings.Personas;
		var claim = new AdminClaim(scoped.GetRequiredService<AccountsOptions>().SetupCode, personas.Admin.Name, personas.Admin.Password, personas.Admin.Password);
		var admin = Require(await scoped.GetRequiredService<IOnboardingService>().ClaimAsync(claim, cancellationToken));
		IssuedLink invite;
		using (users.As(admin))
		{
			await StoreKeysAsync(scoped, settings, cancellationToken);
			await scoped.GetRequiredService<IArrSinkConfigService>().StoreAsync(settings.Arr, cancellationToken);
			Require(await scoped.GetRequiredService<ISinkSwitch>().SwitchAsync(SinkKind.Arr, cancellationToken));
			await AddTitlesAsync(scoped, settings.Seed.Wantlist, cancellationToken);
			await CreateFeedsAsync(scoped, settings.Seed.Feeds, cancellationToken);
			await CreateTargetsAsync(scoped, settings.Seed.Notifications, settings.CatcherUrl, cancellationToken);
			invite = Require(await scoped.GetRequiredService<IUserManagementService>().InviteAsync(personas.Member.Name, cancellationToken));
			await scoped.GetRequiredService<IOnboardingService>().CompleteAsync(cancellationToken);
			await scoped.GetRequiredService<IJobQueue>().EnqueueAsync(new SyncWantlist(new ManualTrigger()), cancellationToken);
		}

		var token = invite.Path[(invite.Path.LastIndexOf('/') + 1)..];
		var member = Require(
			await scoped.GetRequiredService<IAccountLinkService>().JoinAsync(token, personas.Member.Password, personas.Member.Password, cancellationToken)
		);
		using (users.As(member))
		{
			await StoreKeysAsync(scoped, settings, cancellationToken);
		}
	}

	private static async Task StoreKeysAsync(IServiceProvider scoped, StagingSettings settings, CancellationToken cancellationToken)
	{
		var keys = scoped.GetRequiredService<IApiKeyService>();
		await keys.StoreAsync(new SeededKey(ApiKeyProvider.Tmdb, settings.TmdbKey), cancellationToken);
		await settings.MdblistKey.Apply(key => keys.StoreAsync(new SeededKey(ApiKeyProvider.Mdblist, key), cancellationToken));
		await settings.MalKey.Apply(key => keys.StoreAsync(new SeededKey(ApiKeyProvider.Mal, key), cancellationToken));
		await settings.TraktKey.Apply(key => keys.StoreAsync(new SeededKey(ApiKeyProvider.Trakt, key), cancellationToken));
	}

	private static async Task AddTitlesAsync(IServiceProvider scoped, IReadOnlyList<SeedTitle> wantlist, CancellationToken cancellationToken)
	{
		var titles = await scoped.GetRequiredService<IMediaService>().RegisterAsync([.. wantlist.Select(title => title.Ids())], [], cancellationToken);
		var added = await scoped
			.GetRequiredService<IWantlistService>()
			.AddAsync(titles.WhereSome().Select(title => title.Id), new ManualOrigin(ManualSource.Search), cancellationToken);
		if (added.Unaddressable.Count > 0)
		{
			throw new InvalidOperationException($"{added.Unaddressable.Count} seeded wantlist titles have no id the sink can use.");
		}
	}

	private static async Task CreateFeedsAsync(IServiceProvider scoped, IReadOnlyList<SeedFeed> seeded, CancellationToken cancellationToken)
	{
		var feeds = scoped.GetRequiredService<IFeedService>();
		var readiness = scoped.GetRequiredService<IServiceReadiness>();
		foreach (var feed in seeded)
		{
			await feeds
				.CreateAsync(
					new FeedCreateData
					{
						Name = feed.Name,
						IsEnabled = await readiness.ReadyAsync(feed.Provider.Service, cancellationToken),
						Provider = feed.Provider,
						Strategy = feed.FeedStrategy(),
						Identifier = feed.Identifier,
						Limit = Optional.Of(feed.Limit).Map(count => new QueryLimit { Count = count }),
						Schedule = new Manual(),
					},
					cancellationToken
				)
				.GetOrThrow(error => new InvalidOperationException($"The staging feed {feed.Name} was refused: {error.Reason}"));
		}
	}

	private static async Task CreateTargetsAsync(IServiceProvider scoped, SeedNotifications seeded, string catcherUrl, CancellationToken cancellationToken)
	{
		var targets = scoped.GetRequiredService<INotificationTargetRepository>();
		var subscriptions = scoped.GetRequiredService<INotificationSubscriptionRepository>();
		(string Name, NotificationConfig Config)[] configs =
		[
			(seeded.Webhook.Name, new WebhookConfig($"{catcherUrl}/webhook", seeded.Webhook.Secret)),
			(seeded.Pushover.Name, new PushoverConfig(seeded.Pushover.AppToken, seeded.Pushover.UserKey)),
		];
		foreach (var (name, config) in configs)
		{
			var target = await targets.CreateAsync(new NotificationTarget(Guid.NewGuid(), name, true, config), cancellationToken);
			await subscriptions.AddAsync(target, Enum.GetValues<NotificationEventType>(), seeded.Template.ToNotificationTemplate(), cancellationToken);
		}
	}

	private static T Require<T>(Result<T> result)
		where T : notnull
	{
		return result.GetOrThrow(error => new InvalidOperationException(error.Reason));
	}

	private sealed record SeededKey(ApiKeyProvider Provider, string Value) : IApiKey;
}
