using Contracts;
using Contracts.Auth;
using Contracts.Caching;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Access;

internal class CredentialRepository(
	IDbContextFactory<SammlarrDbContext> dbContextFactory,
	ITypedMemoryCache<WatchlistProvider, ICredential> cache,
	ICredentialFactory credentialFactory
) : ICredentialRepository
{
	public async Task<Result<ICredential>> GetAsync(WatchlistProvider watchlistProvider, CancellationToken cancellationToken = default)
	{
		if (cache.TryGetValue(watchlistProvider, out var cached) && cached is not null)
		{
			return cached;
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var credential = await ctx.Credentials.SingleOrDefaultAsync(c => c.WatchlistProvider == watchlistProvider, cancellationToken);
		if (credential is null)
		{
			return Error.From("No credential is stored for this provider.");
		}

		var result = await credentialFactory.CreateAsync(
			credential.WatchlistProvider,
			credential.ClientId,
			credential.AccessToken,
			credential.ExpiresAt,
			cancellationToken
		);
		if (result is ICredential valid)
		{
			cache.Set(watchlistProvider, valid);
		}
		return result;
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
			credential = new CredentialRow
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
		cache.Remove(watchlistProvider);
	}

	public async Task ClearAsync(WatchlistProvider watchlistProvider, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.Credentials.Where(c => c.WatchlistProvider == watchlistProvider).ExecuteDeleteAsync(cancellationToken);
		cache.Remove(watchlistProvider);
	}
}
