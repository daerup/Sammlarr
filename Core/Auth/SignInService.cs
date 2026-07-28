using Contracts;
using Contracts.Auth;
using Contracts.DI;
using Contracts.Identity;
using Contracts.Settings;
using Domain.Identity;
using Domain.Providers;
using Domain.Settings;
using Microsoft.Extensions.Logging;

namespace Core.Auth;

internal sealed class SignInService(
	IKeyedServiceIndex<AuthProvider, IOAuthProvider> providers,
	ICredentialRepository credentials,
	IUserRepository users,
	IUserScope userScope,
	IProviderConfigRepository providerConfig,
	IDateTimeProvider dateTimeProvider,
	ILogger<SignInService> logger
) : ISignInService
{
	public Task<string> BeginAsync(AuthProvider provider, string callbackUrl, CancellationToken cancellationToken = default) =>
		providers[provider].BeginLoginAsync(callbackUrl, cancellationToken);

	public async Task<Result<User>> CompleteAsync(AuthProvider provider, string callbackQuery, CancellationToken cancellationToken = default) =>
		await providers[provider].CompleteLoginAsync(callbackQuery, cancellationToken) switch
		{
			Success<ExternalLogin>(var login) => await ProvisionAsync(provider, login, cancellationToken),
			Error error => OnFailed(provider, error),
		};

	private async Task<Result<User>> ProvisionAsync(AuthProvider provider, ExternalLogin login, CancellationToken cancellationToken)
	{
		if (await users.FindByLoginAsync(provider, login.ExternalUserId, cancellationToken) is Some<User>(var existing))
		{
			return await CompleteSignInAsync(provider, login, existing, isInitialAdmin: false, cancellationToken);
		}

		var isFirstUser = !await users.AnyAsync(cancellationToken);
		if (!isFirstUser && (await providerConfig.GetAsync(cancellationToken)).RegistrationMode == RegistrationMode.Closed)
		{
			logger.LogWarning("Registration is closed; rejected new {Provider} sign-in", provider);
			return Error.From("Registration is closed. Ask an admin for access.");
		}

		var role = isFirstUser ? UserRole.Admin : UserRole.Member;
		var user = new User(UserId.New(), login.DisplayName, login.AvatarUrl, role, dateTimeProvider.Now);
		await users.CreateAsync(user, provider, login.ExternalUserId, cancellationToken);
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
		using (userScope.As(user))
		{
			await credentials.StoreAsync(provider, login.Token.AccessToken, login.Token.ExpiresAt, login.Token.ClientId, cancellationToken);
			if (isInitialAdmin)
			{
				await SetAuthProviderAsync(provider, cancellationToken);
			}
		}

		logger.LogInformation("Sign-in completed for {Provider} as user {UserId}", provider, user.Id);
		return Success.Of(user);
	}

	private async Task SetAuthProviderAsync(AuthProvider provider, CancellationToken cancellationToken)
	{
		var config = await providerConfig.GetAsync(cancellationToken);
		await providerConfig.StoreAsync(config with { AuthProvider = provider }, cancellationToken);
	}

	private Result<User> OnFailed(AuthProvider provider, Error error)
	{
		logger.LogWarning("Sign-in failed for {Provider}: {Reason}", provider, error.Reason);
		return error;
	}
}
