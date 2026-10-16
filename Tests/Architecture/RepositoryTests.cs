using Contracts.Auth;
using Contracts.Blocklist;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Notifications;
using Contracts.Settings;
using Contracts.Setup;
using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using NetArchTest.Rules;

namespace Architecture.Tests;

public class RepositoryTests
{
	private static readonly Type[] UiRepositories =
	[
		typeof(INotificationTargetRepository),
		typeof(INotificationSubscriptionRepository),
		typeof(INotificationDeliveryOutcomeRepository),
	];

	public static IEnumerable<Func<(Type Contract, string[] Consumers)>> AllowedConsumers()
	{
		yield return () =>
			(
				typeof(IWantlistRepository),
				[
					"DB.Data.Wantlist.WantlistRepository",
					"DB.Data.Wantlist.WantlistModule",
					"Core.Wantlist.WantlistService",
					"Core.Wantlist.Sync.WantlistSyncPlanner",
					"Core.Wantlist.Sync.WantlistSyncEngine",
					"Core.Feed.FeedRunPlanner",
					"Core.Feed.FeedService",
				]
			);
		yield return () =>
			(
				typeof(IWantlistOutbox),
				[
					"DB.Data.Wantlist.Outbox.WantlistOutboxRepository",
					"DB.Data.Wantlist.Outbox.OutboxModule",
					"Core.Wantlist.WantlistService",
					"Core.Wantlist.Sync.WantlistSyncPlanner",
					"Core.Wantlist.Sync.WantlistSyncEngine",
				]
			);
		yield return () =>
			(
				typeof(IFeedRepository),
				[
					"DB.Data.Feed.FeedRepository",
					"DB.Data.Feed.FeedModule",
					"Core.Feed.FeedService",
					"Core.Feed.FeedRunEngine",
					"Core.Feed.FeedRunPlanner",
					"Messaging.Notifications.NotificationContextFactory",
					"Messaging.Notifications.NotificationDispatchHandler",
				]
			);
		yield return () =>
			(typeof(IBlocklistRepository), ["DB.Data.Blocklist.BlocklistRepository", "DB.Data.Blocklist.BlocklistModule", "Core.Blocklist.BlocklistService"]);
		yield return () =>
			(
				typeof(IMediaRepository),
				[
					"DB.Data.Media.MediaRepository",
					"DB.Data.Media.MediaModule",
					"Core.Media.*",
					"Messaging.Notifications.NotificationContextFactory",
					"Messaging.Notifications.NotificationDispatchHandler",
				]
			);
		yield return () =>
			(
				typeof(IApiKeyConfigRepository),
				[
					"DB.Data.Auth.ApiKeyConfigRepository",
					"DB.Data.Auth.AuthDataModule",
					"Core.Auth.ApiKeyService",
					"Core.Providers.ServiceReadiness",
					"API.Mdblist.MdblistRequestsLeft",
				]
			);
		yield return () =>
			(
				typeof(ICredentialRepository),
				["DB.Data.Auth.CredentialRepository", "DB.Data.Auth.AuthDataModule", "Core.Auth.CredentialService", "Core.Providers.ServiceReadiness"]
			);
		yield return () =>
			(typeof(IArrSinkConfigRepository), ["DB.Data.Config.ArrSinkConfigRepository", "DB.Data.Config.ConfigModule", "Core.Settings.ArrSinkConfigService"]);
		yield return () =>
			(
				typeof(ILocalAccountRepository),
				[
					"DB.Data.Identity.LocalAccountRepository",
					"DB.Data.Identity.IdentityDataModule",
					"Core.Auth.SignInService",
					"Core.Auth.LocalAccountService",
					"Core.Auth.AdminPasswordReset",
					"Core.Auth.AccountLinkService",
					"Core.Identity.UserManagementService",
					"Core.Setup.OnboardingService",
				]
			);
		yield return () => (typeof(ISetupStateRepository), ["DB.Data.Config.SetupStateRepository", "DB.Data.Config.ConfigModule", "Core.Setup.OnboardingService"]);
	}

	[Test]
	public async Task Dependencies_OfUi_ReachNoRepositoryButTheNotificationSettingsAndNotTheOutbox()
	{
		var forbidden = Projects
			.Contracts.GetTypes()
			.Where(type => type.IsInterface && (type.Name.EndsWith("Repository", StringComparison.Ordinal) || type == typeof(IWantlistOutbox)))
			.Except(UiRepositories)
			.Select(type => type.FullName!)
			.ToArray();

		await Assert.That(Types.InAssembly(Projects.Ui).That().HaveDependencyOnAny([.. UiRepositories.Select(type => type.FullName!)]).GetTypes()).IsNotEmpty();
		await Types.InAssembly(Projects.Ui).Should().NotHaveDependencyOnAny(forbidden).GetResult().AssertNone();
	}

	[Test]
	[Arguments("DB.Data.Feed", new[] { "DB.Data.Wantlist", "DB.Data.Blocklist" })]
	[Arguments("DB.Data.Blocklist", new[] { "DB.Data.Wantlist", "DB.Data.Feed" })]
	[Arguments("DB.Data.Wantlist", new[] { "DB.Data.Blocklist" })]
	public async Task Dependencies_OfAnAggregatesPersistence_LeaveTheOtherAggregatesTablesAlone(string aggregate, string[] others)
	{
		await Assert.That(Types.InAssembly(Projects.Db).That().ResideInNamespace(aggregate).GetTypes()).IsNotEmpty();
		await Types.InAssembly(Projects.Db).That().ResideInNamespace(aggregate).Should().NotHaveDependencyOnAny(others).GetResult().AssertNone();
	}

	[Test]
	[MethodDataSource(nameof(AllowedConsumers))]
	public async Task Dependencies_OnARepository_ComeOnlyFromItsAllowedConsumers(Type contract, string[] consumers)
	{
		var found = Consumers(contract).ToList();

		await Assert.That(found).Contains(consumers[0]);
		await found.Where(consumer => !consumers.Any(allowed => Allows(allowed, consumer))).AssertNone();
	}

	private static bool Allows(string allowed, string consumer) =>
		allowed.EndsWith(".*", StringComparison.Ordinal) ? consumer.StartsWith(allowed[..^1], StringComparison.Ordinal) : consumer == allowed;

	private static IEnumerable<string> Consumers(Type contract) =>
		Types
			.InAssemblies(Projects.Consumers)
			.That()
			.HaveDependencyOnAny(contract.FullName!)
			.GetTypes()
			.Select(type => type.FullName.Split('/', '+')[0])
			.Distinct();
}
