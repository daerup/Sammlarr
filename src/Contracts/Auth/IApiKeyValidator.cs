using Domain.Auth;

namespace Contracts.Auth;

public interface IApiKeyValidator
{
	Task<AccessCheck> CheckAsync(Secret value, CancellationToken cancellationToken = default);
}
