namespace Contracts.Auth;

public interface IApiKeyFactory
{
	Task<Result<IApiKey>> CreateAsync(ApiKeyProvider provider, string value, CancellationToken cancellationToken = default);
}
