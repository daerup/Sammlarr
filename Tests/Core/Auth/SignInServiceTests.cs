using Contracts;
using Contracts.Auth;
using Contracts.Identity;
using Contracts.Notifications;
using Contracts.Settings;
using Core.Auth;
using Core.Notifications;
using Domain.Identity;
using Domain.Providers;
using Domain.Settings;
using Microsoft.Extensions.Logging;
using TestInfrastructure;

namespace Core.Tests.Auth;

public class SignInServiceTests
{
	private static readonly ExternalLogin Login = new("plex-uuid", "Zoe", "avatar", new OAuthTokenGrant("token", DateTimeOffset.UnixEpoch, Guid.Empty));

	private readonly Mock<IOAuthProvider> _plex = IOAuthProvider.Mock();
	private readonly Mock<IUserRepository> _users = IUserRepository.Mock();
	private readonly Mock<IProviderConfigService> _config = IProviderConfigService.Mock();
	private readonly Mock<ILogger<SignInService>> _logger = ILogger<SignInService>.Mock();

	private SignInService Testee
	{
		get
		{
			return new SignInService(
				new FakeKeyedServiceIndex<AuthProvider, IOAuthProvider>().Register(AuthProvider.Plex, _plex.Object),
				ICredentialService.Mock().Object,
				_users.Object,
				ILocalAccountRepository.Mock().Object,
				IUserScope.Mock().Object,
				_config.Object,
				IDateTimeProvider.Mock().Object,
				new SignInGate(),
				new WebUiNotificationSeeder(INotificationTargetRepository.Mock().Object, INotificationSubscriptionRepository.Mock().Object),
				_logger.Object
			);
		}
	}

	[Test]
	public async Task CompleteAsync_WhenTheProviderLoginFails_ReturnsItsReasonAndWarnsThatTheSignInFailed()
	{
		_config.GetAsync(Any()).Returns(Config(RegistrationMode.Open));
		_users.GetAllAsync(Any()).Returns([]);
		_plex.CompleteLoginAsync(Any(), Any()).Returns(Error.From("denied"));

		var result = await Testee.CompleteAsync(AuthProvider.Plex, "callback");

		await Assert.That(result).IsError().And.Multiple(async error => await Assert.That(error.Reason).IsEqualTo("denied"));
		await Assert.That(Warnings()).IsEquivalentTo(["Sign-in failed for Plex: denied"]);
	}

	[Test]
	public async Task CompleteAsync_WhenRegistrationIsClosed_WarnsOnlyThatItIsClosed()
	{
		_plex.CompleteLoginAsync(Any(), Any()).Returns(Success.Of(Login));
		_users.FindByLoginAsync(Any<AuthProvider>(), Any(), Any()).Returns(new None());
		_users.GetAllAsync(Any()).Returns([new User(UserId.New(), "Ada", "avatar", UserRole.Admin, DateTimeOffset.UnixEpoch)]);
		_config.GetAsync(Any()).Returns(Config(RegistrationMode.Closed));

		var result = await Testee.CompleteAsync(AuthProvider.Plex, "callback");

		await Assert.That(result).IsError();
		await Assert.That(Warnings()).IsEquivalentTo(["Registration is closed; rejected new Plex sign-in"]);
	}

	private static ProviderConfig Config(RegistrationMode registration)
	{
		return new ProviderConfig
		{
			Sink = SinkKind.Plex,
			TrendingProvider = TrendingProvider.Tmdb,
			SearchProvider = SearchProvider.Plex,
			AuthProvider = AuthProvider.Plex,
			RegistrationMode = registration,
		};
	}

	private IEnumerable<string> Warnings()
	{
		return Mock.Invocations(_logger).Where(call => call.Arguments[0] is LogLevel.Warning).Select(call => $"{call.Arguments[2]}");
	}
}
