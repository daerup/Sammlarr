using Contracts.Providers;
using Domain.Providers;

namespace Core.Tests.Media;

internal static class TestCapacity
{
	internal static IRemoteCapacity Open() => With(_ => new RemoteLoad(new None(), 0));

	internal static IRemoteCapacity With(Func<RemoteService, RemoteLoad> load)
	{
		var capacity = IRemoteCapacity.Mock();
		capacity.Of(Any()).Returns(remote => load(remote));
		return capacity.Object;
	}
}
