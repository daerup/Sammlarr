using Domain.Auth;
using Domain.Providers;

namespace Contracts.Auth;

public interface ICredentialFactory
{
	Task<Result<ICredential>> CreateAsync(
		AuthProvider provider,
		Guid clientId,
		string accessToken,
		DateTimeOffset expiresAt,
		CancellationToken cancellationToken = default
	);
}
