namespace Contracts.Settings;

public interface IProviderSettingsRepository
{
	ValueTask<ProviderConfig> GetAsync(CancellationToken cancellationToken = default);

	Task StoreAsync(ProviderConfig providerConfig, CancellationToken cancellationToken = default);
}
