using Core.Caching;
using Core.Media;
using Domain.Identity;
using Microsoft.Extensions.Caching.Memory;
using TestInfrastructure;

namespace Core.Tests.Media;

[NotInParallel("typed-memory-cache-clear")]
public sealed class UnmatchedMediaCacheTests : IDisposable
{
	private const string Unmatched = "Tvdb|tmdb:1396|Tmdb";

	private readonly MemoryCache _memory = new(new MemoryCacheOptions());
	private readonly TypedMemoryCache<string, bool> _cache;

	public UnmatchedMediaCacheTests()
	{
		_cache = new TypedMemoryCache<string, bool>(_memory);
		_cache.Set(Unmatched, true);
	}

	public void Dispose() => _memory.Dispose();

	[Test]
	public async Task Clear_WhenAdmin_ForgetsTheUnmatchedTitles()
	{
		var testee = new UnmatchedMediaCache(_cache, new TestCurrentUser { Role = UserRole.Admin });

		testee.Clear();

		await Assert.That(_cache.GetValueOrNone(Unmatched)).IsNone();
	}

	[Test]
	public async Task Clear_WhenMember_ThrowsUnauthorizedAndKeepsTheUnmatchedTitles()
	{
		var testee = new UnmatchedMediaCache(_cache, new TestCurrentUser { Role = UserRole.Member });

		await Assert.That(() => testee.Clear()).Throws<UnauthorizedAccessException>();

		await Assert.That(_cache.GetValueOrNone(Unmatched)).IsSome();
	}
}
