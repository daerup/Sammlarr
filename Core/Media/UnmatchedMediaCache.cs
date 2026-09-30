using Contracts.Caching;
using Contracts.Identity;
using Contracts.Media;

namespace Core.Media;

internal sealed class UnmatchedMediaCache(ITypedMemoryCache<string, bool> cache, ICurrentUser currentUser) : IUnmatchedMediaCache
{
	public void Clear()
	{
		currentUser.EnsureAdmin();
		cache.Clear();
	}
}
