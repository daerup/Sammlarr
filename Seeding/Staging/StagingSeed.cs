using Contracts.Auth;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Jobs;
using Contracts.Media;
using Contracts.Notifications;
using Contracts.Settings;
using Contracts.Setup;
using Contracts.Wantlist;
using Core.Setup;
using DB;
using Domain.Auth;
using Domain.Feed;
using Domain.Jobs;
using Domain.Media;
using Domain.Notifications;
using Domain.Processing;
using Domain.Providers;
using Domain.Wantlist;
using Domain.Wantlist.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Seeding.Staging;

internal enum StagingScenario
{
	Configured,
	Fresh,
}

internal sealed record StagingSettings(string TmdbKey, Optional<string> MdblistKey, ArrSinkConfig Arr, string CatcherUrl);

internal static class StagingPersonas
{
	internal const string Admin = "admin";

	internal const string AdminPassword = "staging-admin-passphrase";

	internal const string Member = "member";

	internal const string MemberPassword = "staging-member-passphrase";
}

internal static class StagingSeed
{
	private static readonly IExternalMediaId[] ProjectHailMary = [TmdbId.From("687163", MediaType.Movie)];

	private static readonly IExternalMediaId[] BreakingBad = [TmdbId.From("1396", MediaType.Show), TvdbId.From("81189", MediaType.Show)];

	internal static async Task SeedAsync(IServiceProvider services, StagingScenario scenario, StagingSettings settings, CancellationToken cancellationToken)
	{
		await using var scope = services.CreateAsyncScope();
		var scoped = scope.ServiceProvider;
		await using (var context = await scoped.GetRequiredService<IDbContextFactory<SammlarrDbContext>>().CreateDbContextAsync(cancellationToken))
		{
			await context.Database.MigrateAsync(cancellationToken);
		}

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
		var claim = new AdminClaim(
			scoped.GetRequiredService<SetupGate>().Code,
			StagingPersonas.Admin,
			StagingPersonas.AdminPassword,
			StagingPersonas.AdminPassword
		);
		var admin = Require(await scoped.GetRequiredService<IOnboardingService>().ClaimAsync(claim, cancellationToken));
		IssuedLink invite;
		using (users.As(admin))
		{
			await StoreKeysAsync(scoped, settings, cancellationToken);
			await scoped.GetRequiredService<IArrSinkConfigService>().StoreAsync(settings.Arr, cancellationToken);
			Require(await scoped.GetRequiredService<ISinkSwitch>().SwitchAsync(SinkKind.Arr, cancellationToken));
			await AddTitlesAsync(scoped, cancellationToken);
			await CreateFeedsAsync(scoped, settings.MdblistKey.IsSome(), cancellationToken);
			await CreateTargetsAsync(scoped, settings.CatcherUrl, cancellationToken);
			invite = Require(await scoped.GetRequiredService<IUserManagementService>().InviteAsync(StagingPersonas.Member, cancellationToken));
			await scoped.GetRequiredService<IOnboardingService>().CompleteAsync(cancellationToken);
			await scoped.GetRequiredService<IJobQueue>().EnqueueAsync(new SyncWantlist(new ManualTrigger()), cancellationToken);
		}

		var token = invite.Path[(invite.Path.LastIndexOf('/') + 1)..];
		var member = Require(
			await scoped.GetRequiredService<IAccountLinkService>().JoinAsync(token, StagingPersonas.MemberPassword, StagingPersonas.MemberPassword, cancellationToken)
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
	}

	private static async Task AddTitlesAsync(IServiceProvider scoped, CancellationToken cancellationToken)
	{
		var titles = await scoped.GetRequiredService<IMediaService>().RegisterAsync([ProjectHailMary, BreakingBad], cancellationToken);
		await scoped.GetRequiredService<IWantlistService>().AddAsync(titles.Select(title => title.Id), new ManualOrigin(ManualSource.Search), cancellationToken);
	}

	private static async Task CreateFeedsAsync(IServiceProvider scoped, bool enabled, CancellationToken cancellationToken)
	{
		var feeds = scoped.GetRequiredService<IFeedService>();
		await feeds.CreateAsync(
			new FeedCreateData
			{
				Name = "Top movies of the week",
				IsEnabled = enabled,
				Provider = FeedProvider.Mdblist,
				Strategy = new FeedStrategy(new Append()),
				Identifier = "2236",
				Schedule = new Manual(),
			},
			cancellationToken
		);
		await feeds.CreateAsync(
			new FeedCreateData
			{
				Name = "Trending shows",
				IsEnabled = enabled,
				Provider = FeedProvider.Mdblist,
				Strategy = new FeedStrategy(new Mirror()),
				Identifier = "88434",
				Limit = Some.Of(new QueryLimit { Count = 10 }),
				Schedule = new Manual(),
			},
			cancellationToken
		);
	}

	private static async Task CreateTargetsAsync(IServiceProvider scoped, string catcherUrl, CancellationToken cancellationToken)
	{
		var targets = scoped.GetRequiredService<INotificationTargetRepository>();
		var subscriptions = scoped.GetRequiredService<INotificationSubscriptionRepository>();
		NotificationConfig[] configs = [new WebhookConfig($"{catcherUrl}/webhook", "whsec_staging"), new PushoverConfig("staging-app-token", "staging-user-key")];
		foreach (var config in configs)
		{
			var name = config is WebhookConfig ? "Catcher webhook" : "Catcher Pushover";
			var target = await targets.CreateAsync(new NotificationTarget(Guid.NewGuid(), name, true, config), cancellationToken);
			await subscriptions.AddAsync(target, Enum.GetValues<NotificationEventType>(), NotificationTemplate.None, cancellationToken);
		}
	}

	private static T Require<T>(Result<T> result)
		where T : notnull
	{
		return result.GetOrThrow(error => new InvalidOperationException(error.Reason));
	}

	private sealed record SeededKey(ApiKeyProvider Provider, string Value) : IApiKey;
}
