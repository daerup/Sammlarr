using Contracts.Providers;
using Domain.Providers;

namespace Core.Tests.Media;

internal static class TestCapacity
{
	internal static IRemoteCapacity Open() => With(_ => RemoteLoad.Idle);

	internal static IRemoteCapacity With(Func<RemoteService, RemoteLoad> load)
	{
		var capacity = IRemoteCapacity.Mock();
		capacity.Of(Any()).Returns(remote => load(remote));
		return capacity.Object;
	}
}
