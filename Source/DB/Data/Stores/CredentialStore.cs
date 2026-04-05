using Contracts.Auth;
using Contracts.Caching;
using DB.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Stores;

internal class CredentialStore(SammlarrDbContext context, ITypedMemoryCache<AuthProvider, Credential> cache) : ICredentialStore
{
	public async ValueTask<StoredCredential?> GetAsync(AuthProvider authProvider, CancellationToken cancellationToken = default)
	{
		if (cache.TryGetValue(authProvider, out var cached))
		{
			return new StoredCredential(cached!.AuthProvider, cached.ClientId, cached.AccessToken, cached.ExpiresAt);
		}

		var credential = await context.Credentials.FirstOrDefaultAsync(c => c.AuthProvider == authProvider, cancellationToken);
		if (credential is null)
		{
			return null;
		}
		return new StoredCredential(credential.AuthProvider, credential.ClientId, credential.AccessToken, credential.ExpiresAt);
	}

	public async Task StoreAsync(
		AuthProvider authProvider,
		string accessToken,
		DateTimeOffset accessTokenExpiresAt,
		Guid clientId,
		CancellationToken cancellationToken = default
	)
	{
		var credential = await context.Credentials.FirstOrDefaultAsync(c => c.AuthProvider == authProvider, cancellationToken);
		if (credential is null)
		{
			credential = new Credential
			{
				AuthProvider = authProvider,
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
		cache.Set(authProvider, credential);
	}

	public async Task ClearAsync(AuthProvider authProvider, CancellationToken cancellationToken = default)
	{
		var credential = await context.Credentials.FirstOrDefaultAsync(c => c.AuthProvider == authProvider, cancellationToken);
		if (credential is null)
		{
			return;
		}

		context.Credentials.Remove(credential);
		await context.SaveChangesAsync(cancellationToken);
		cache.Remove(authProvider);
	}
}
