namespace Contracts.Media;

public interface IApiKeyStore
{
	Task<string?> GetApiKeyAsync(MediaProvider provider, CancellationToken cancellationToken = default);

	Task SetApiKeyAsync(MediaProvider provider, string apiKey, CancellationToken cancellationToken = default);

	Task ClearApiKeyAsync(MediaProvider provider, CancellationToken cancellationToken = default);
}
