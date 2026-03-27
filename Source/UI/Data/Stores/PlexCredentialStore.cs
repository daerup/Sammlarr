using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UI.Data;
using UI.Data.Entities;

namespace UI.Data.Stores;

public sealed class PlexCredentialStore(SammlarrDbContext context) : IPlexCredentialStore
{
	public Task<PlexCredential?> GetAsync(CancellationToken cancellationToken = default)
	{
		return context.PlexCredentials.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task StoreAsync(string accessToken, string? refreshToken, DateTimeOffset? accessTokenExpiresAt, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			throw new ArgumentException("Access token is required.", nameof(accessToken));
		}

		var credential = await context.PlexCredentials.FirstOrDefaultAsync(cancellationToken);
		if (credential is null)
		{
			credential = new PlexCredential
			{
				AccessToken = accessToken,
				RefreshToken = refreshToken ?? string.Empty,
				AccessTokenExpiresAt = accessTokenExpiresAt,
			};
			context.PlexCredentials.Add(credential);
		}
		else
		{
			credential.AccessToken = accessToken;
			if (!string.IsNullOrWhiteSpace(refreshToken))
			{
				credential.RefreshToken = refreshToken;
			}

			credential.AccessTokenExpiresAt = accessTokenExpiresAt;
		}

		await context.SaveChangesAsync(cancellationToken);
	}

	public async Task ClearAsync(CancellationToken cancellationToken = default)
	{
		var credential = await context.PlexCredentials.FirstOrDefaultAsync(cancellationToken);
		if (credential is null)
		{
			return;
		}

		context.PlexCredentials.Remove(credential);
		await context.SaveChangesAsync(cancellationToken);
	}
}
