using Contracts.Auth;
using Contracts.Caching;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Access;

internal class CredentialRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory, ITypedMemoryCache<WatchlistProvider, CredentialEntity> cache)
	: ICredentialStore
{
	public async ValueTask<StoredCredential?> GetAsync(WatchlistProvider watchlistProvider, CancellationToken cancellationToken = default)
	{
		if (cache.TryGetValue(watchlistProvider, out var cached))
		{
			return new StoredCredential(cached!.WatchlistProvider, cached.ClientId, cached.AccessToken, cached.ExpiresAt);
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var credential = await ctx.Credentials.FirstOrDefaultAsync(c => c.WatchlistProvider == watchlistProvider, cancellationToken);
		if (credential is null)
		{
			return null;
		}
		return new StoredCredential(credential.WatchlistProvider, credential.ClientId, credential.AccessToken, credential.ExpiresAt);
	}

	public async Task StoreAsync(
		WatchlistProvider watchlistProvider,
		string accessToken,
		DateTimeOffset accessTokenExpiresAt,
		Guid clientId,
		CancellationToken cancellationToken = default
	)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var credential = await ctx.Credentials.FirstOrDefaultAsync(c => c.WatchlistProvider == watchlistProvider, cancellationToken);
		if (credential is null)
		{
			credential = new CredentialEntity
			{
				WatchlistProvider = watchlistProvider,
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
		cache.Set(watchlistProvider, credential);
	}

	public async Task ClearAsync(WatchlistProvider watchlistProvider, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var credential = await ctx.Credentials.FirstOrDefaultAsync(c => c.WatchlistProvider == watchlistProvider, cancellationToken);
		if (credential is null)
		{
			return;
		}

		ctx.Credentials.Remove(credential);
		await ctx.SaveChangesAsync(cancellationToken);
		cache.Remove(watchlistProvider);
	}
}
