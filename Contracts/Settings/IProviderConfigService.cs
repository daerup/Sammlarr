using Domain.Settings;

namespace Contracts.Settings;

public interface IProviderConfigService
{
	ValueTask<ProviderConfig> GetAsync(CancellationToken cancellationToken = default);

	Task StoreAsync(ProviderConfig config, CancellationToken cancellationToken = default);
}
