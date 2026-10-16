using Domain.Auth;
using Domain.Providers;

namespace Contracts.Auth;

public interface ICredentialService
{
	ValueTask<Result<ICredential>> GetAsync(AuthProvider provider, CancellationToken cancellationToken = default);

	Task<bool> IsStoredAsync(AuthProvider provider, CancellationToken cancellationToken = default);

	Task<Result<ICredential>> TestAsync(AuthProvider provider, StoredCredential credential, CancellationToken cancellationToken = default);

	Task StoreAsync(AuthProvider provider, StoredCredential credential, CancellationToken cancellationToken = default);

	Task ClearAsync(AuthProvider provider, CancellationToken cancellationToken = default);

	void ReportRejected(AuthProvider provider);
}
