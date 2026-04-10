namespace Contracts.Settings;

public interface IProviderSettingsStore
{
	ValueTask<ProviderSettings> GetAsync(CancellationToken cancellationToken = default);

	Task StoreAsync(ProviderSettings providerSettings, CancellationToken cancellationToken = default);
}
