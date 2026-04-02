using Contracts.Auth;
using DB.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Stores;

public class CredentialStore(SammlarrDbContext context) : Contracts.Auth.ICredentialStore
{
	public async Task<StoredCredential?> GetAsync(AuthProvider authProvider, CancellationToken cancellationToken = default)
	{
		var credential = await context.Credentials.FirstOrDefaultAsync(c => c.AuthProvider == authProvider, cancellationToken);
		return credential is null ? null : new StoredCredential(credential.AuthProvider, credential.ClientId, credential.AccessToken, credential.ExpiresAt);
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
		}

		await context.SaveChangesAsync(cancellationToken);
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
	}
}
