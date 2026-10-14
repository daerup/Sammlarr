using Contracts.Providers;

namespace Core.Tests.Media;

internal static class TestReadiness
{
	internal static IServiceReadiness Everything() => EverythingMock().Object;

	private static Mock<IServiceReadiness> EverythingMock()
	{
		var readiness = IServiceReadiness.Mock();
		readiness.ConnectedAsync(Any(), Any()).Returns((asked, _) => asked.ToHashSet());
		return readiness;
	}
}
