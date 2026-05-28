namespace Contracts.Auth;

public interface IApiKeyValidator
{
	Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);
}
