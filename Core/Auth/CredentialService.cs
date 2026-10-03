using Contracts.Auth;
using Domain.Auth;
using Domain.Providers;

namespace Core.Auth;

internal sealed class CredentialService(ICredentialRepository credentials, ICredentialFactory factory, SecretCache<AuthProvider, ICredential> cache)
	: ICredentialService
{
	public ValueTask<Result<ICredential>> GetAsync(AuthProvider provider, CancellationToken cancellationToken = default)
	{
		return cache.GetOrLoadAsync(
			provider,
			() =>
				credentials
					.GetAsync(provider, cancellationToken)
					.Map(stored => factory.CreateAsync(provider, stored.ClientId, stored.AccessToken, stored.ExpiresAt, cancellationToken))
		);
	}

	public Task<bool> IsStoredAsync(AuthProvider provider, CancellationToken cancellationToken = default)
	{
		return credentials.GetAsync(provider, cancellationToken).IsSuccess();
	}

	public async Task StoreAsync(AuthProvider provider, StoredCredential credential, CancellationToken cancellationToken = default)
	{
		await credentials.StoreAsync(provider, credential.AccessToken, credential.ExpiresAt, credential.ClientId, cancellationToken);
		cache.Evict(provider);
	}

	public async Task ClearAsync(AuthProvider provider, CancellationToken cancellationToken = default)
	{
		await credentials.ClearAsync(provider, cancellationToken);
		cache.Evict(provider);
	}

	public void ReportRejected(AuthProvider provider)
	{
		cache.Evict(provider);
	}
}
