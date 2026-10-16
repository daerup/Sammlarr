using Contracts.Auth;
using Domain.Providers;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Auth;

internal sealed class CredentialRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : ICredentialRepository
{
	public async Task<Result<StoredCredential>> GetAsync(AuthProvider provider, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx
			.Credentials.SingleResultAsync(c => c.Provider == provider, "No credential is stored for this provider.", cancellationToken)
			.Map(credential => new StoredCredential(credential.AccessToken, credential.ExpiresAt, credential.ClientId));
	}

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
	}

	public async Task ClearAsync(AuthProvider provider, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		await ctx.Credentials.Where(c => c.Provider == provider).ExecuteDeleteAsync(cancellationToken);
	}
}
