using Domain.Auth;

namespace Contracts.Auth;

public interface ICredentialValidator
{
	Task<AccessCheck> CheckAsync(Guid clientId, string accessToken, CancellationToken cancellationToken = default);
}
