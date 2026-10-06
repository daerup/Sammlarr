using Domain.Providers;

namespace Contracts.Providers;

public interface IRemoteCapacity
{
	RemoteLoad Of(RemoteService remote);
}
