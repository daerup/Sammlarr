using Domain.Providers;
using Domain.Wantlist.Arr;

namespace Contracts.Wantlist;

public interface ISinkSwitch
{
	Task<Result<SinkSwitched>> SwitchAsync(SinkKind sink, CancellationToken cancellationToken = default);

	Task<Result<SinkSwitched>> MoveAsync(ArrSinkConfig arr, CancellationToken cancellationToken = default);
}

public sealed record SinkSwitched(int Queued, int Unaddressable);
