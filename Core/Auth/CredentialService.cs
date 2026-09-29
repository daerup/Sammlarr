using Contracts.Auth;
using Contracts.Caching;
using Contracts.Identity;
using Domain.Auth;
using Domain.Providers;

namespace Core.Auth;

internal sealed class CredentialService(
	ICredentialRepository credentials,
	ICredentialFactory factory,
	ITypedMemoryCache<(Guid User, AuthProvider Provider), ICredential> cache,
	ICurrentUser currentUser
) : ICredentialService
{
	private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(10);

	private (Guid User, AuthProvider Provider) CacheKey(AuthProvider provider) => (currentUser.IsAuthenticated ? currentUser.Id.Value : Guid.Empty, provider);

	public async ValueTask<Result<ICredential>> GetAsync(AuthProvider provider, CancellationToken cancellationToken = default)
	{
		if (cache.TryGetValue(CacheKey(provider), out var cached) && cached is not null)
		{
			return Success.Of(cached);
		}

		return await credentials
			.GetAsync(provider, cancellationToken)
			.Map(stored => factory.CreateAsync(provider, stored.ClientId, stored.AccessToken, stored.ExpiresAt, cancellationToken))
			.Apply(valid => cache.Set(CacheKey(provider), valid, CacheLifetime));
	}

	public async Task<bool> HasCredentialAsync(AuthProvider provider, CancellationToken cancellationToken = default) =>
		await GetAsync(provider, cancellationToken).IsSuccess();

	public Task<Result<ICredential>> TestAsync(
		AuthProvider provider,
		Guid clientId,
		string accessToken,
		DateTimeOffset expiresAt,
		CancellationToken cancellationToken = default
	) => factory.CreateAsync(provider, clientId, accessToken, expiresAt, cancellationToken);

	public async Task StoreAsync(
		AuthProvider provider,
		string accessToken,
		DateTimeOffset accessTokenExpiresAt,
		Guid clientId,
		CancellationToken cancellationToken = default
	)
	{
		await credentials.StoreAsync(provider, accessToken, accessTokenExpiresAt, clientId, cancellationToken);
		cache.Remove(CacheKey(provider));
	}

	public async Task ClearAsync(AuthProvider provider, CancellationToken cancellationToken = default)
	{
		await credentials.ClearAsync(provider, cancellationToken);
		cache.Remove(CacheKey(provider));
	}

	public void Invalidate(AuthProvider provider) => cache.Remove(CacheKey(provider));
}
