using System.Buffers.Text;
using System.Diagnostics;
using System.Net;
using System.Text;
using Contracts.Auth;
using Contracts.Identity;
using Contracts.Notifications;
using Contracts.Settings;
using Contracts.Setup;
using Core.Auth;
using Core.Setup;
using Domain.Identity;
using Domain.Notifications;
using Domain.Providers;
using Domain.Wantlist.Arr;
using Harness;
using Integration.Tests.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Shared.Options;
using TestInfrastructure;

namespace Integration.Tests.Secrets;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class SecretSweepTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string Announcement = "No admin yet. Open /setup and enter setup code {SetupCode}";

	private readonly string _canary = $"canary-{Guid.NewGuid():N}";

	private Secret Canary => new(_canary);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Control_ACanaryWrittenToEachPlace_IsFound()
	{
		var capture = host.CaptureTelemetry();
		using var source = new ActivitySource("Sammlarr.Sweep");

		host.Resolve<ILoggerFactory>().CreateLogger("Sammlarr.Sweep").LogWarning("Control {Value}", $"{_canary}-log");
		source.StartActivity("control")!.SetTag("value", $"{_canary}-span").Dispose();
		await host.Resolve<INotificationTargetRepository>().CreateAsync(new NotificationTarget(Guid.Empty, _canary, true, new WebUiConfig()));

		await Assert.That(capture.Written(Every)).Contains(text => text.Contains($"{_canary}-log", StringComparison.Ordinal));
		await Assert.That(capture.Written(Every)).Contains(text => text.Contains($"{_canary}-span", StringComparison.Ordinal));
		await Assert.That(host.DatabaseHolds(_canary)).IsTrue();
	}

	[Test]
	[Arguments(ApiKeyProvider.Tmdb, TestUrls.TmdbApi)]
	[Arguments(ApiKeyProvider.TmdbReadToken, TestUrls.TmdbApi)]
	[Arguments(ApiKeyProvider.Mdblist, TestUrls.MdblistApi)]
	[Arguments(ApiKeyProvider.Mal, TestUrls.Mal)]
	[Arguments(ApiKeyProvider.Trakt, TestUrls.Trakt)]
	public async Task ApiKey_RefusedByItsService_IsSentButNeverWritten(ApiKeyProvider provider, string address)
	{
		var capture = host.CaptureTelemetry();
		host.HttpStub.RespondWithStatusForUrl(address, HttpStatusCode.Unauthorized);

		await host.Resolve<IApiKeyService>().StoreAsync(new TestApiKey(provider, Canary));
		await Assert.That(await host.Resolve<IApiKeyService>().TestAsync(provider, Canary)).IsError();

		await Assert.That(host.HttpStub.Saw(_canary)).IsTrue();
		await NeverWrittenAsync(capture, Every, _canary);
	}

	[Test]
	[Arguments(ArrApp.Radarr, "radarr.test.com:7878")]
	[Arguments(ArrApp.Sonarr, "sonarr.test.com:8989")]
	public async Task ArrKey_RefusedByTheApp_IsSentButNeverWritten(ArrApp app, string address)
	{
		var capture = host.CaptureTelemetry();
		host.HttpStub.RespondWithStatusForUrl(address, HttpStatusCode.Unauthorized);
		var instance = ArrInstanceConfig.Empty with { Url = Some.Of($"http://{address}"), ApiKey = Some.Of(Canary) };

		await host.Resolve<IArrSinkConfigService>().StoreAsync(ArrSinkConfig.Empty.With(app, instance));
		await Assert.That(await host.Resolve<IArrConnectionTester>().TestAsync(app, instance)).IsError();

		await Assert.That(host.HttpStub.Saw(_canary)).IsTrue();
		await NeverWrittenAsync(capture, Every, _canary);
	}

	[Test]
	public async Task PlexToken_RefusedByPlex_IsSentButNeverWritten()
	{
		var capture = host.CaptureTelemetry();
		host.HttpStub.RespondWithStatusForUrl(TestUrls.PlexApi, HttpStatusCode.Unauthorized);
		var credential = new StoredCredential(Canary, DateTimeOffset.UtcNow.AddDays(30), Guid.NewGuid());

		await host.Resolve<ICredentialService>().StoreAsync(AuthProvider.Plex, credential);
		await Assert.That(await host.Resolve<ICredentialService>().TestAsync(AuthProvider.Plex, credential)).IsError();

		await Assert.That(host.HttpStub.Saw(_canary)).IsTrue();
		await NeverWrittenAsync(capture, Every, _canary);
	}

	[Test]
	[Arguments(NotificationChannel.Webhook, TestUrls.Webhook)]
	[Arguments(NotificationChannel.Pushover, TestUrls.Pushover)]
	public async Task NotificationSecrets_RefusedByTheService_AreSentButNeverWritten(NotificationChannel channel, string address)
	{
		var capture = host.CaptureTelemetry();
		var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(_canary));
		host.HttpStub.RespondWithStatusForUrl(address, HttpStatusCode.Unauthorized);
		NotificationConfig config = channel switch
		{
			NotificationChannel.Webhook => new WebhookConfig(new Secret($"https://{address}/{_canary}"), new Secret($"whsec_{encoded}")),
			NotificationChannel.Pushover => new PushoverConfig(Canary, new Secret($"{_canary}-user")),
			NotificationChannel.WebUi => new WebUiConfig(),
		};
		var target = new NotificationTarget(Guid.Empty, "Hook", true, config);

		await host.Resolve<INotificationTargetRepository>().CreateAsync(target);
		var delivered = await host.ResolveKeyed<INotificationChannel>(channel).SendAsync(Context(), target, NotificationTemplate.None);

		await Assert.That(host.HttpStub.Saw(_canary)).IsTrue();
		await Assert
			.That(delivered)
			.IsOfUnionType<DeliveryResult, DeliveryUnrecoverable>()
			.And.Multiple(async refused => await Assert.That(refused.Reason).DoesNotContain(_canary));
		await NeverWrittenAsync(capture, Every, _canary, encoded);
	}

	[Test]
	public async Task SetupCodeAndAdminPassword_ClaimingTheInstall_AreNeverWritten()
	{
		var capture = host.CaptureTelemetry();
		await host.NoUsers();
		var gate = host.Resolve<SetupGate>();

		await new SetupCodeAnnouncer(host.Resolve<IServiceProvider>(), gate, host.Resolve<ILoggerFactory>()).StartAsync(CancellationToken.None);
		var claimed = await host.Resolve<IOnboardingService>().ClaimAsync(new AdminClaim(gate.Code, "ada", Canary, Canary));

		await Assert.That(claimed).IsSuccess();
		await Assert.That(await host.Resolve<ISignInService>().SignInWithPasswordAsync("ada", Canary)).IsSuccess();
		await Assert.That(capture.Logs.GetSnapshot()).Contains(record => Template(record) == Announcement);
		await NeverWrittenAsync(capture, record => Template(record) != Announcement, _canary, gate.Code.Reveal(), gate.Code.Reveal().Replace("-", string.Empty));
	}

	[Test]
	public async Task InviteTokenAndPassword_JoiningThroughTheLink_AreNeverWritten()
	{
		var capture = host.CaptureTelemetry();
		await host.SignsInWithSammlarrAccounts();
		var link = IssuedLinkParts.Of(await host.Resolve<IUserManagementService>().InviteAsync("mira"));

		var joined = await host.Resolve<IAccountLinkService>().JoinAsync(link.User, link.Token, Canary, Canary);

		await Assert.That(joined).IsSuccess();
		await NeverWrittenAsync(capture, Every, _canary, link.Token.Reveal(), Decoded(link.Token));
	}

	[Test]
	public async Task ResetTokenAndPassword_ResettingThroughTheLink_AreNeverWritten()
	{
		var capture = host.CaptureTelemetry();
		await host.SignsInWithSammlarrAccounts();
		var guest = await host.SeedLocalUser("guest", new Secret("correct horse battery"), UserRole.Member);
		var link = IssuedLinkParts.Of(await host.Resolve<IUserManagementService>().ResetLinkAsync(guest));

		var reset = await host.Resolve<IAccountLinkService>().ResetPasswordAsync(link.User, link.Token, Canary, Canary);

		await Assert.That(reset).IsSuccess();
		await NeverWrittenAsync(capture, Every, _canary, link.Token.Reveal(), Decoded(link.Token));
	}

	[Test]
	public async Task ResetAdminPassword_AppliedAtStartup_IsNeverWritten()
	{
		var capture = host.CaptureTelemetry();
		await host.SignsInWithSammlarrAccounts();
		await host.SeedLocalUser("owner", new Secret("correct horse battery"), UserRole.Admin);
		var options = new AccountsOptions { ResetAdminPassword = Canary };

		await new AdminPasswordReset(host.Resolve<IServiceProvider>(), options, host.Resolve<ILogger<AdminPasswordReset>>()).StartAsync(CancellationToken.None);

		await Assert.That(await host.Resolve<ISignInService>().SignInWithPasswordAsync("owner", Canary)).IsSuccess();
		await NeverWrittenAsync(capture, Every, _canary);
	}

	private static bool Every(FakeLogRecord record) => true;

	private static string Decoded(Secret token) => Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token.Reveal()));

	private static string? Template(FakeLogRecord record) => record.GetStructuredStateValue("{OriginalFormat}");

	private async Task NeverWrittenAsync(TelemetryCapture capture, Func<FakeLogRecord, bool> counted, params string[] secrets)
	{
		var written = capture.Written(counted).ToList();
		foreach (var secret in secrets)
		{
			await Assert.That(written.Where(text => text.Contains(secret, StringComparison.Ordinal))).IsEmpty();
			await Assert.That(host.DatabaseHolds(secret)).IsFalse();
		}
	}

	private static NotificationContext Context()
	{
		return new(
			Guid.NewGuid(),
			NotificationEventType.WantlistSyncCompleted,
			NotificationLevel.Success,
			DateTimeOffset.UnixEpoch,
			"Wantlist sync completed",
			"Synced.",
			new NotificationData()
		);
	}
}
