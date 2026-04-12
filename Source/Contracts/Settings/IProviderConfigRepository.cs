namespace Contracts.Settings;

public interface IProviderConfigRepository
{
	ValueTask<ProviderConfig> GetAsync(CancellationToken cancellationToken = default);

	Task StoreAsync(ProviderConfig providerConfig, CancellationToken cancellationToken = default);
}
