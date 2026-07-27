using Contracts;
using Contracts.Auth;
using Contracts.Caching;
using Contracts.Identity;
using Domain.Auth;
using Domain.Providers;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Access;

internal sealed class CredentialRepository(
	IDbContextFactory<SammlarrDbContext> dbContextFactory,
	ITypedMemoryCache<(Guid User, AuthProvider Provider), ICredential> cache,
	ICredentialFactory credentialFactory,
	ICurrentUser currentUser
) : ICredentialRepository
{
	private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(10);

	private (Guid User, AuthProvider Provider) CacheKey(AuthProvider provider) => (currentUser.IsAuthenticated ? currentUser.Id.Value : Guid.Empty, provider);

	public async ValueTask<Result<ICredential>> GetAsync(AuthProvider provider, CancellationToken cancellationToken = default)
	{
		if (cache.TryGetValue(CacheKey(provider), out var cached) && cached is not null)
		{
			return Success.Of(cached);
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx
			.Credentials.SingleResultAsync(c => c.Provider == provider, "No credential is stored for this provider.", cancellationToken)
			.Map(credential =>
				credentialFactory.CreateAsync(credential.Provider, credential.ClientId, credential.AccessToken, credential.ExpiresAt, cancellationToken)
			)
			.Apply(valid => cache.Set(CacheKey(provider), valid, CacheLifetime));
	}

	public void Invalidate(AuthProvider provider) => cache.Remove(CacheKey(provider));

	public async Task StoreAsync(
		AuthProvider provider,
		string accessToken,
		DateTimeOffset accessTokenExpiresAt,
		Guid clientId,
		CancellationToken cancellationToken = default
	)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var credential = await ctx.Credentials.FirstOrDefaultAsync(c => c.Provider == provider, cancellationToken);
		if (credential is null)
		{
			credential = new CredentialRow
			{
				Provider = provider,
				AccessToken = accessToken,
				ExpiresAt = accessTokenExpiresAt,
				ClientId = clientId,
			};
			ctx.Credentials.Add(credential);
		}
		else
		{
			credential.AccessToken = accessToken;
			credential.ExpiresAt = accessTokenExpiresAt;
			credential.ClientId = clientId;
		}

		await ctx.SaveChangesAsync(cancellationToken);
		cache.Remove(CacheKey(provider));
	}

	public async Task ClearAsync(AuthProvider provider, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.Credentials.Where(c => c.Provider == provider).ExecuteDeleteAsync(cancellationToken);
		cache.Remove(CacheKey(provider));
	}
}
