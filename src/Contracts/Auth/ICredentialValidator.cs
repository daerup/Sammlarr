using Domain.Auth;

namespace Contracts.Auth;

public interface ICredentialValidator
{
	Task<AccessCheck> CheckAsync(Guid clientId, Secret accessToken, CancellationToken cancellationToken = default);
}
