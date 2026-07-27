using Contracts.Auth;
using Contracts.DI;
using Contracts.Identity;
using Contracts.Settings;
using Domain.Identity;
using Domain.Providers;
using Microsoft.Extensions.Logging;

namespace Core.Auth;

internal sealed class SignInService(
	IKeyedServiceIndex<AuthProvider, IOAuthProvider> providers,
	ICredentialRepository credentials,
	IUserRepository users,
	IUserScope userScope,
	IProviderConfigRepository providerConfig,
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
		var (user, isInitialOwner) = await FindOrCreateAsync(provider, login, cancellationToken);
		using (userScope.As(user))
		{
			await credentials.StoreAsync(provider, login.Token.AccessToken, login.Token.ExpiresAt, login.Token.ClientId, cancellationToken);
			if (isInitialOwner)
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

	private async Task<(User User, bool IsInitialOwner)> FindOrCreateAsync(AuthProvider provider, ExternalLogin login, CancellationToken cancellationToken)
	{
		if (await users.FindByLoginAsync(provider, login.ExternalUserId, cancellationToken) is Some<User>(var existing))
		{
			return (existing, false);
		}

		var isFirstUser = !await users.AnyAsync(cancellationToken);
		var role = isFirstUser ? UserRole.Owner : UserRole.Member;
		var user = new User(UserId.New(), login.DisplayName, login.AvatarUrl, role);
		await users.CreateAsync(user, provider, login.ExternalUserId, cancellationToken);
		return (user, isFirstUser);
	}

	private Result<User> OnFailed(AuthProvider provider, Error error)
	{
		logger.LogWarning("Sign-in failed for {Provider}: {Reason}", provider, error.Reason);
		return error;
	}
}
