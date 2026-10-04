namespace Contracts.Settings;

public interface IArrSinkConfigService
{
	Task<ArrSinkConfig> GetAsync(CancellationToken cancellationToken = default);

	Task StoreAsync(ArrSinkConfig config, CancellationToken cancellationToken = default);
}
