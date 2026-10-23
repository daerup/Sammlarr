using Contracts.Auth;
using Contracts.DI;
using Contracts.Identity;
using Contracts.Settings;
using Core.Notifications;
using Core.Setup;
using Domain.Identity;
using Domain.Providers;
using Domain.Settings;
using Microsoft.Extensions.Logging;

namespace Core.Auth;

internal sealed class SignInService(
	IKeyedServiceIndex<AuthProvider, IOAuthProvider> providers,
	ICredentialService credentials,
	IUserRepository users,
	ILocalAccountRepository localAccounts,
	IUserScope userScope,
	IProviderConfigService providerConfig,
	TimeProvider clock,
	SetupGate setupGate,
	WebUiNotificationSeeder webUiNotifications,
	ILogger<SignInService> logger
) : ISignInService
{
	private static readonly Error WrongPassword = Error.From("Wrong username or password.");

	private static readonly Error Locked = Error.From(Lockout.Message);

	private static readonly Error NoAdminYet = Error.From("This Sammlarr has no admin yet. Open /setup and enter the setup code from the server log.");

	public Task<Result<string>> BeginAsync(AuthProvider provider, string callbackUrl, CancellationToken cancellationToken = default) =>
		providers[provider].BeginLoginAsync(callbackUrl, cancellationToken);

	public Task<Result<User>> CompleteAsync(AuthProvider provider, string callbackQuery, CancellationToken cancellationToken = default)
	{
		return CompleteAsync(provider, callbackQuery, new None(), cancellationToken);
	}

	public Task<Result<User>> CompleteSetupAsync(AuthProvider provider, string callbackQuery, Secret setupCode, CancellationToken cancellationToken = default)
	{
		return CompleteAsync(provider, callbackQuery, Some.Of(setupCode), cancellationToken);
	}

	private async Task<Result<User>> CompleteAsync(AuthProvider provider, string callbackQuery, Optional<Secret> setupCode, CancellationToken cancellationToken)
	{
		var config = await providerConfig.GetAsync(cancellationToken);
		if (config.AuthProvider != provider && await users.CountAsync(cancellationToken) > 0)
		{
			return Error.From($"This Sammlarr signs in with {SignInName(config.AuthProvider)}.");
		}

		return await providers[provider]
			.CompleteLoginAsync(callbackQuery, cancellationToken)
			.ApplyError(error => LogFailed(provider, error))
			.Map(login => ProvisionAsync(provider, login, setupCode, cancellationToken));
	}

	public async Task<Result<User>> LinkAsync(AuthProvider provider, string callbackQuery, UserId signedIn, CancellationToken cancellationToken = default)
	{
		var config = await providerConfig.GetAsync(cancellationToken);
		if (config.AuthProvider == provider)
		{
			return await CompleteAsync(provider, callbackQuery, cancellationToken);
		}

		return await providers[provider]
			.CompleteLoginAsync(callbackQuery, cancellationToken)
			.ApplyError(error => LogFailed(provider, error))
			.Map(login => StoreLinkAsync(provider, login, signedIn, cancellationToken));
	}

	private async Task<Result<User>> StoreLinkAsync(AuthProvider provider, ExternalLogin login, UserId signedIn, CancellationToken cancellationToken)
	{
		var user = await users.GetAsync(signedIn, cancellationToken);
		using (userScope.As(user))
		{
			await credentials.StoreAsync(provider, new StoredCredential(login.Token.AccessToken, login.Token.ExpiresAt, login.Token.ClientId), cancellationToken);
		}

		logger.LogInformation("Linked {Provider} to user {UserId}", provider, user.Id);
		return Success.Of(user);
	}

	public async Task<Result<User>> SignInWithPasswordAsync(string username, Secret password, CancellationToken cancellationToken = default)
	{
		var config = await providerConfig.GetAsync(cancellationToken);
		if (config.AuthProvider != AuthProvider.Local)
		{
			return Error.From($"This Sammlarr signs in with {SignInName(config.AuthProvider)}.");
		}

		var attempt = await localAccounts.CheckPasswordAsync(username, password, cancellationToken);
		return attempt switch
		{
			PasswordAccepted accepted => Passed(accepted.User),
			AccountLockedOut => LockedOut(username),
			PasswordRejected => Rejected(username),
		};
	}

	private Result<User> Passed(User user)
	{
		logger.LogInformation("Sign-in completed for {Provider} as user {UserId}", AuthProvider.Local, user.Id);
		return Success.Of(user);
	}

	private Result<User> LockedOut(string username)
	{
		logger.LogWarning("Sign-in refused for {Provider} user {Username}: the account is locked out", AuthProvider.Local, username.ReplaceLineEndings(" "));
		return Locked;
	}

	private Result<User> Rejected(string username)
	{
		logger.LogWarning("Sign-in failed for {Provider} user {Username}: wrong username or password", AuthProvider.Local, username.ReplaceLineEndings(" "));
		return WrongPassword;
	}

	private static string SignInName(AuthProvider provider)
	{
		return provider switch
		{
			AuthProvider.Plex => "Plex",
			AuthProvider.Local => "Sammlarr accounts",
		};
	}

	private async Task<Result<User>> ProvisionAsync(AuthProvider provider, ExternalLogin login, Optional<Secret> setupCode, CancellationToken cancellationToken)
	{
		return await users
			.FindByLoginAsync(provider, login.ExternalUserId, cancellationToken)
			.Match(
				existing => CompleteSignInAsync(provider, login, existing, isInitialAdmin: false, cancellationToken),
				() => RegisterAsync(provider, login, setupCode, cancellationToken)
			);
	}

	private async Task<Result<User>> RegisterAsync(AuthProvider provider, ExternalLogin login, Optional<Secret> setupCode, CancellationToken cancellationToken)
	{
		using var lease = await setupGate.EnterAsync(cancellationToken);
		var isFirstUser = await users.CountAsync(cancellationToken) == 0;
		if (isFirstUser && !setupCode.Match(setupGate.Accepts, () => false))
		{
			logger.LogWarning("Rejected a first {Provider} sign-in without the setup code", provider);
			return NoAdminYet;
		}

		if (!isFirstUser && (await providerConfig.GetAsync(cancellationToken)).RegistrationMode == RegistrationMode.Closed)
		{
			logger.LogWarning("Registration is closed; rejected new {Provider} sign-in", provider);
			return Error.From("Registration is closed. Ask an admin for access.");
		}

		var role = isFirstUser ? UserRole.Admin : UserRole.Member;
		var user = new User(UserId.New(), login.DisplayName, login.AvatarUrl, role, clock.GetUtcNow());
		await users.CreateAsync(user, provider, login.ExternalUserId, cancellationToken);
		setupGate.SawAdmin();
		using (userScope.As(user))
		{
			await webUiNotifications.SeedAsync(cancellationToken);
		}

		return await CompleteSignInAsync(provider, login, user, isInitialAdmin: isFirstUser, cancellationToken);
	}

	private async Task<Result<User>> CompleteSignInAsync(
		AuthProvider provider,
		ExternalLogin login,
		User user,
		bool isInitialAdmin,
		CancellationToken cancellationToken
	)
	{
		var refreshed = user with { DisplayName = login.DisplayName, AvatarUrl = login.AvatarUrl };
		using (userScope.As(refreshed))
		{
			await credentials.StoreAsync(provider, new StoredCredential(login.Token.AccessToken, login.Token.ExpiresAt, login.Token.ClientId), cancellationToken);
			if (isInitialAdmin)
			{
				await SetAuthProviderAsync(provider, cancellationToken);
			}
		}

		await users.RefreshAsync(refreshed.Id, refreshed.DisplayName, refreshed.AvatarUrl, cancellationToken);
		logger.LogInformation("Sign-in completed for {Provider} as user {UserId}", provider, user.Id);
		return Success.Of(refreshed);
	}

	private async Task SetAuthProviderAsync(AuthProvider provider, CancellationToken cancellationToken)
	{
		var config = await providerConfig.GetAsync(cancellationToken);
		await providerConfig.StoreAsync(config with { AuthProvider = provider, RegistrationMode = RegistrationMode.Closed }, cancellationToken);
	}

	private void LogFailed(AuthProvider provider, Error error) => logger.LogWarning("Sign-in failed for {Provider}: {Reason}", provider, error.Reason);
}
