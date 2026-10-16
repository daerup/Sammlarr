using Domain.Auth;

namespace Contracts.Auth;

public interface IApiKeyValidator
{
	Task<AccessCheck> CheckAsync(string value, CancellationToken cancellationToken = default);
}
