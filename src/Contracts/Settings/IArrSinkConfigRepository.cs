using Domain.Wantlist.Arr;

namespace Contracts.Settings;

public interface IArrSinkConfigRepository
{
	Task<ArrSinkConfig> GetAsync(CancellationToken cancellationToken = default);

	Task StoreAsync(ArrSinkConfig config, CancellationToken cancellationToken = default);
}
