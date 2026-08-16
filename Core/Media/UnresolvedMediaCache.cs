using Contracts.Caching;
using Contracts.Identity;
using Contracts.Media;

namespace Core.Media;

internal sealed class UnresolvedMediaCache(ITypedMemoryCache<string, bool> cache, ICurrentUser currentUser) : IUnresolvedMediaCache
{
	public void Clear()
	{
		currentUser.EnsureAdmin();
		cache.Clear();
	}
}
