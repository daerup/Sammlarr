using Contracts.Auth;
using Contracts.DI;
using Domain.Auth;
using Domain.Providers;
using Domain.Usage;
using Observability.Usage;

namespace Core.Auth;

internal sealed class CredentialService(
	ICredentialRepository credentials,
	IKeyedServiceSet<AuthProvider, ICredentialValidator> validators,
	SecretCache<AuthProvider, ICredential> cache
) : ICredentialService
{
	public ValueTask<Result<ICredential>> GetAsync(AuthProvider provider, CancellationToken cancellationToken = default)
	{
		return cache.GetOrLoadAsync(
			provider,
			() => credentials.GetAsync(provider, cancellationToken).Map(stored => CredentialOf(provider, stored)),
			credential => CheckAsync(provider, credential.ClientId, credential.AccessToken, cancellationToken)
		);
	}

	public Task<bool> IsStoredAsync(AuthProvider provider, CancellationToken cancellationToken = default)
	{
		return credentials.GetAsync(provider, cancellationToken).IsSuccess();
	}

	public async Task<Result<ICredential>> TestAsync(AuthProvider provider, StoredCredential credential, CancellationToken cancellationToken = default)
	{
		using var marked = UsageCauses.Mark(UsageCause.KeyTest);
		var check = await CheckAsync(provider, credential.ClientId, credential.AccessToken, cancellationToken);
		return AccessChecks.Answer(check, CredentialOf(provider, credential), $"{provider}", "token");
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
		cache.Reject(provider);
	}

	private Task<AccessCheck> CheckAsync(AuthProvider provider, Guid clientId, string accessToken, CancellationToken cancellationToken)
	{
		return validators
			.Find(provider)
			.Match(validator => validator.CheckAsync(clientId, accessToken, cancellationToken), () => Task.FromResult(AccessCheck.Valid));
	}

	private static ICredential CredentialOf(AuthProvider provider, StoredCredential stored)
	{
		return new SignInCredential(provider, stored.ClientId, stored.AccessToken, stored.ExpiresAt);
	}
}
