using Contracts.Auth;
using Contracts.Caching;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Access;

internal class CredentialStore(SammlarrDbContext context, ITypedMemoryCache<WatchlistProvider, Credential> cache) : ICredentialStore
{
	public async ValueTask<StoredCredential?> GetAsync(WatchlistProvider watchlistProvider, CancellationToken cancellationToken = default)
	{
		if (cache.TryGetValue(watchlistProvider, out var cached))
		{
			return new StoredCredential(cached!.WatchlistProvider, cached.ClientId, cached.AccessToken, cached.ExpiresAt);
		}

		var credential = await context.Credentials.FirstOrDefaultAsync(c => c.WatchlistProvider == watchlistProvider, cancellationToken);
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
		var credential = await context.Credentials.FirstOrDefaultAsync(c => c.WatchlistProvider == watchlistProvider, cancellationToken);
		if (credential is null)
		{
			credential = new Credential
			{
				WatchlistProvider = watchlistProvider,
				AccessToken = accessToken,
				ExpiresAt = accessTokenExpiresAt,
				ClientId = clientId,
			};
			context.Credentials.Add(credential);
		}
		else
		{
			credential.AccessToken = accessToken;
			credential.ExpiresAt = accessTokenExpiresAt;
			credential.ClientId = clientId;
		}

		await context.SaveChangesAsync(cancellationToken);
		cache.Set(watchlistProvider, credential);
	}

	public async Task ClearAsync(WatchlistProvider watchlistProvider, CancellationToken cancellationToken = default)
	{
		var credential = await context.Credentials.FirstOrDefaultAsync(c => c.WatchlistProvider == watchlistProvider, cancellationToken);
		if (credential is null)
		{
			return;
		}

		context.Credentials.Remove(credential);
		await context.SaveChangesAsync(cancellationToken);
		cache.Remove(watchlistProvider);
	}
}
