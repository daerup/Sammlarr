using Contracts.Caching;
using TestInfrastructure;

namespace Core.Tests.Media;

internal static class TestUnmatchedCache
{
	internal static Mock<ITypedMemoryCache<string, bool>> Empty()
	{
		var cache = ITypedMemoryCache<string, bool>.Mock();
		cache.GetValueOrNone(Any()).Returns(new None());
		return cache;
	}
}
