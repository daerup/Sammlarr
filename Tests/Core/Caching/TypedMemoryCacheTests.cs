using Core.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace Core.Tests.Caching;

public class TypedMemoryCacheUnitTests
{
	[Test]
	public async Task TypedMemoryCache_WhenKeyDoesNotExist_ReturnsFalseAndNullValue()
	{
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var testee = new TypedMemoryCache<string, string>(memoryCache);

		var found = testee.TryGetValue("missing", out var value);

		await Assert.That(found).IsFalse();
		await Assert.That(value).IsNull();
	}

	[Test]
	public async Task TypedMemoryCache_WhenSetAndGetCalled_ReturnsStoredValue()
	{
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var testee = new TypedMemoryCache<string, string>(memoryCache);

		testee.Set("key", "value");
		var found = testee.TryGetValue("key", out var value);

		await Assert.That(found).IsTrue();
		await Assert.That(value).IsEqualTo("value");
	}

	[Test]
	public async Task TypedMemoryCache_WhenKeyAlreadyExists_ReturnsLatestStoredValue()
	{
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var testee = new TypedMemoryCache<string, string>(memoryCache);

		testee.Set("key", "value-1");
		testee.Set("key", "value-2");
		var found = testee.TryGetValue("key", out var value);

		await Assert.That(found).IsTrue();
		await Assert.That(value).IsEqualTo("value-2");
	}

	[Test]
	public async Task TypedMemoryCache_WhenRemoveCalled_KeyIsNoLongerFound()
	{
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var testee = new TypedMemoryCache<string, string>(memoryCache);

		testee.Set("key", "value");
		testee.Remove("key");
		var found = testee.TryGetValue("key", out _);

		await Assert.That(found).IsFalse();
	}

	[Test]
	public async Task TypedMemoryCache_WhenRemovingMissingKey_DoesNothing()
	{
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var testee = new TypedMemoryCache<string, string>(memoryCache);

		testee.Remove("missing");
		var found = testee.TryGetValue("missing", out var value);

		await Assert.That(found).IsFalse();
		await Assert.That(value).IsNull();
	}

	[Test]
	public async Task TypedMemoryCache_WhenTwoCachesShareKeyButDifferInValueType_DoNotCollide()
	{
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var credentials = new TypedMemoryCache<string, string>(memoryCache);
		var counts = new TypedMemoryCache<string, int>(memoryCache);

		credentials.Set("plex", "credential");
		counts.Set("plex", 42);

		var credentialFound = credentials.TryGetValue("plex", out var credential);
		var countFound = counts.TryGetValue("plex", out var count);

		await Assert.That(credentialFound).IsTrue();
		await Assert.That(credential).IsEqualTo("credential");
		await Assert.That(countFound).IsTrue();
		await Assert.That(count).IsEqualTo(42);
	}

	[Test]
	public async Task TypedMemoryCache_WhenSetWithTtl_ExpiresAfterDuration()
	{
		using var memoryCache = new MemoryCache(new MemoryCacheOptions { ExpirationScanFrequency = TimeSpan.FromMilliseconds(1) });
		var testee = new TypedMemoryCache<string, string>(memoryCache);

		testee.Set("key", "value", TimeSpan.FromMilliseconds(10));
		var foundImmediately = testee.TryGetValue("key", out var initial);
		await Task.Delay(100);
		var foundAfterExpiry = testee.TryGetValue("key", out var expired);

		await Assert.That(foundImmediately).IsTrue();
		await Assert.That(initial).IsEqualTo("value");
		await Assert.That(foundAfterExpiry).IsFalse();
		await Assert.That(expired).IsNull();
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

		await Assert.That(testee.TryGetValue("first", out _)).IsFalse();
		await Assert.That(testee.TryGetValue("second", out _)).IsFalse();
	}

	[Test]
	[NotInParallel("typed-memory-cache-clear")]
	public async Task TypedMemoryCache_WhenClearCalled_EvictsEntriesSetWithTtl()
	{
		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var testee = new TypedMemoryCache<string, bool>(memoryCache);

		testee.Set("ttl", true, TimeSpan.FromHours(1));
		testee.Clear();

		await Assert.That(testee.TryGetValue("ttl", out _)).IsFalse();
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

		await Assert.That(flags.TryGetValue("shared", out _)).IsFalse();
		var countFound = counts.TryGetValue("shared", out var count);
		await Assert.That(countFound).IsTrue();
		await Assert.That(count).IsEqualTo(7);
	}
}
