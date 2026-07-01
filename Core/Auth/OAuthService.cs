using Contracts;
using Contracts.Auth;
using Contracts.Caching;
using Contracts.DI;
using Contracts.Settings;
using Domain.Auth;

namespace Core.Auth;

internal sealed class OAuthService(
	IKeyedServiceIndex<WatchlistProvider, IOAuthProvider> providers,
	IProviderConfigRepository configRepository,
	ICredentialRepository credentialRepository,
	ITypedMemoryCache<WatchlistProvider, AuthorizedUser> userCache
) : IOAuthService
{
	private static readonly TimeSpan UserCacheTtl = TimeSpan.FromMinutes(5);

	public async Task<string> BeginLoginAsync(string callbackUrl, CancellationToken cancellationToken = default) =>
		await (await ResolveAsync(cancellationToken)).BeginLoginAsync(callbackUrl, cancellationToken);

	public async Task<Result<None>> CompleteLoginAsync(string callbackQuery, CancellationToken cancellationToken = default)
	{
		var config = await configRepository.GetAsync(cancellationToken);
		var outcome = await providers[config.WatchlistProvider].CompleteLoginAsync(callbackQuery, cancellationToken);
		return outcome switch
		{
			AuthGrant grant => await StoreAsync(config.WatchlistProvider, grant, cancellationToken),
			Error error => error,
		};
	}

	public async Task LogoutAsync(CancellationToken cancellationToken = default)
	{
		var config = await configRepository.GetAsync(cancellationToken);
		await credentialRepository.ClearAsync(config.WatchlistProvider, cancellationToken);
		userCache.Remove(config.WatchlistProvider);
	}

	public async Task<AuthSession> GetUserAsync(CancellationToken cancellationToken = default)
	{
		var config = await configRepository.GetAsync(cancellationToken);
		if (userCache.TryGetValue(config.WatchlistProvider, out var cached) && cached is not null)
		{
			return cached;
		}

		if (await credentialRepository.GetAsync(config.WatchlistProvider, cancellationToken) is not ICredential)
		{
			return new SignedOut();
		}

		var session = await providers[config.WatchlistProvider].GetUserAsync(cancellationToken);
		if (session is AuthorizedUser user)
		{
			userCache.Set(config.WatchlistProvider, user, UserCacheTtl);
		}
		return session;
	}

	private async Task<Result<None>> StoreAsync(WatchlistProvider provider, AuthGrant grant, CancellationToken cancellationToken)
	{
		await credentialRepository.StoreAsync(provider, grant.AccessToken, grant.ExpiresAt, grant.ClientId, cancellationToken);
		userCache.Remove(provider);
		return new None();
	}

	private async Task<IOAuthProvider> ResolveAsync(CancellationToken cancellationToken = default)
	{
		var config = await configRepository.GetAsync(cancellationToken);
		return providers[config.WatchlistProvider];
	}
}
