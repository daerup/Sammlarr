using Core.Caching;
using Microsoft.Extensions.Caching.Memory;
using TestInfrastructure;

namespace Core.Tests.Caching;

public class TypedMemoryCacheTests
{
	[Test]
	public async Task TypedMemoryCache_WhenKeyDoesNotExist_ReturnsNone()
	{
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var testee = new TypedMemoryCache<string, string>(memoryCache);

		await Assert.That(testee.GetValueOrNone("missing")).IsNone();
	}

	[Test]
	public async Task TypedMemoryCache_WhenSetAndGetCalled_ReturnsStoredValue()
	{
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var testee = new TypedMemoryCache<string, string>(memoryCache);

		testee.Set("key", "value");
		await Assert.That(testee.GetValueOrNone("key")).IsSome().And.IsEqualTo("value");
	}

	[Test]
	[NotInParallel("typed-memory-cache-clear")]
	public async Task TypedMemoryCache_WhenTheStoredValueIsTheDefault_StillFindsIt()
	{
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var testee = new TypedMemoryCache<string, bool>(memoryCache);

		testee.Set("key", false);

		await Assert.That(testee.GetValueOrNone("key")).IsSome().And.IsFalse();
	}

	[Test]
	public async Task TypedMemoryCache_WhenKeyAlreadyExists_ReturnsLatestStoredValue()
	{
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var testee = new TypedMemoryCache<string, string>(memoryCache);

		testee.Set("key", "value-1");
		testee.Set("key", "value-2");
		await Assert.That(testee.GetValueOrNone("key")).IsSome().And.IsEqualTo("value-2");
	}

	[Test]
	public async Task TypedMemoryCache_WhenRemoveCalled_KeyIsNoLongerFound()
	{
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var testee = new TypedMemoryCache<string, string>(memoryCache);

		testee.Set("key", "value");
		testee.Remove("key");
		await Assert.That(testee.GetValueOrNone("key")).IsNone();
	}

	[Test]
	public async Task TypedMemoryCache_WhenRemovingMissingKey_DoesNothing()
	{
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var testee = new TypedMemoryCache<string, string>(memoryCache);

		testee.Remove("missing");
		await Assert.That(testee.GetValueOrNone("missing")).IsNone();
	}

	[Test]
	public async Task TypedMemoryCache_WhenTwoCachesShareKeyButDifferInValueType_DoNotCollide()
	{
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var credentials = new TypedMemoryCache<string, string>(memoryCache);
		var counts = new TypedMemoryCache<string, int>(memoryCache);

		credentials.Set("plex", "credential");
		counts.Set("plex", 42);

		await Assert.That(credentials.GetValueOrNone("plex")).IsSome().And.IsEqualTo("credential");
		await Assert.That(counts.GetValueOrNone("plex")).IsSome().And.IsEqualTo(42);
	}

	[Test]
	public async Task TypedMemoryCache_WhenSetWithTtl_ExpiresAfterDuration()
	{
		using var memoryCache = new MemoryCache(new MemoryCacheOptions { ExpirationScanFrequency = TimeSpan.FromMilliseconds(1) });
		var testee = new TypedMemoryCache<string, string>(memoryCache);

		testee.Set("key", "value", TimeSpan.FromMilliseconds(10));
		var immediately = testee.GetValueOrNone("key");
		await Task.Delay(100);
		var afterExpiry = testee.GetValueOrNone("key");

		await Assert.That(immediately).IsSome().And.IsEqualTo("value");
		await Assert.That(afterExpiry).IsNone();
	}

	[Test]
	[NotInParallel("typed-memory-cache-clear")]
	public async Task TypedMemoryCache_WhenClearCalled_EvictsEveryEntryOfThatType()
	{
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var testee = new TypedMemoryCache<string, bool>(memoryCache);

		testee.Set("first", true);
		testee.Set("second", false);
		testee.Clear();

		await Assert.That(testee.GetValueOrNone("first")).IsNone();
		await Assert.That(testee.GetValueOrNone("second")).IsNone();
	}

	[Test]
	[NotInParallel("typed-memory-cache-clear")]
	public async Task TypedMemoryCache_WhenClearCalled_EvictsEntriesSetWithTtl()
	{
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var testee = new TypedMemoryCache<string, bool>(memoryCache);

		testee.Set("ttl", true, TimeSpan.FromHours(1));
		testee.Clear();

		await Assert.That(testee.GetValueOrNone("ttl")).IsNone();
	}

	[Test]
	[NotInParallel("typed-memory-cache-clear")]
	public async Task TypedMemoryCache_WhenClearCalled_LeavesOtherValueTypesUntouched()
	{
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var flags = new TypedMemoryCache<string, bool>(memoryCache);
		var counts = new TypedMemoryCache<string, int>(memoryCache);

		flags.Set("shared", true);
		counts.Set("shared", 7);
		flags.Clear();

		await Assert.That(flags.GetValueOrNone("shared")).IsNone();
		await Assert.That(counts.GetValueOrNone("shared")).IsSome().And.IsEqualTo(7);
	}
}
