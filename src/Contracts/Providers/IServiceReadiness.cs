using Domain.Providers;

namespace Contracts.Providers;

public interface IServiceReadiness
{
	Task<IReadOnlySet<RemoteService>> ConnectedAsync(IEnumerable<RemoteService> remotes, CancellationToken cancellationToken = default);

	Task<bool> ReadyAsync(Service service, CancellationToken cancellationToken = default);
}
