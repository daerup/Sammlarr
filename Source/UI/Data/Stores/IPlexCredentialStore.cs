using System;
using System.Threading;
using System.Threading.Tasks;
using UI.Data.Entities;

namespace UI.Data.Stores;

public interface IPlexCredentialStore
{
	Task<PlexCredential?> GetAsync(CancellationToken cancellationToken = default);

	Task StoreAsync(string accessToken, string? refreshToken, DateTimeOffset? accessTokenExpiresAt, CancellationToken cancellationToken = default);

	Task ClearAsync(CancellationToken cancellationToken = default);
}
