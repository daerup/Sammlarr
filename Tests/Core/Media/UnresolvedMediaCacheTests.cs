using Contracts.Caching;
using Core.Media;
using Domain.Identity;
using TestInfrastructure;

namespace Core.Tests.Media;

public class UnresolvedMediaCacheTests
{
	[Test]
	public async Task Clear_WhenAdmin_DelegatesToTypedCacheOnce()
	{
		var cache = ITypedMemoryCache<string, bool>.Mock();
		var testee = new UnresolvedMediaCache(cache.Object, new TestCurrentUser { Role = UserRole.Admin });

		await Assert.That(() => testee.Clear()).ThrowsNothing();

		cache.Clear().WasCalled(Times.Once);
	}

	[Test]
	public async Task Clear_WhenMember_ThrowsUnauthorizedAndNeverTouchesCache()
	{
		var cache = ITypedMemoryCache<string, bool>.Mock();
		var testee = new UnresolvedMediaCache(cache.Object, new TestCurrentUser { Role = UserRole.Member });

		await Assert.That(() => testee.Clear()).Throws<UnauthorizedAccessException>();

		cache.Clear().WasNeverCalled();
	}
}
