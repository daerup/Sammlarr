using Contracts.Auth;
using DB.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Stores;

public sealed class CredentialStore(SammlarrDbContext context) : ICredentialStore
{
	public Task<Credential?> GetAsync(AuthProvider authProvider, CancellationToken cancellationToken = default)
	{
		return context.Credentials.FirstOrDefaultAsync(c => c.AuthProvider == authProvider, cancellationToken);
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
