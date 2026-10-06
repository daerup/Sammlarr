using Contracts.Providers;
using Domain.Providers;

namespace UI.Tests.Providers;

internal static class ReadinessStub
{
	internal static IServiceReadiness Connected(IReadOnlySet<RemoteService> connected)
	{
		var readiness = IServiceReadiness.Mock();
		readiness.ConnectedAsync(Any(), Any()).Returns((asked, _) => asked.Where(connected.Contains).ToHashSet());
		readiness.ReadyAsync(Any(), Any()).Returns((service, _) => service.Remotes.All(connected.Contains));
		return readiness.Object;
	}
}
