using Contracts.Caching;
using Core;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Core.Tests.Integration.Caching;

public class TypedMemoryCacheIntegrationTests
{
	[Test]
	public async Task TypedMemoryCache_WhenSetAndGetCalled_ReturnsStoredValue()
	{
		var services = new ServiceCollection();
		services.AddModule<CoreModule>();
		using var serviceProvider = services.BuildServiceProvider();
		using var scope = serviceProvider.CreateScope();
		var testee = scope.ServiceProvider.GetRequiredService<ITypedMemoryCache<string, string>>();

		testee.Set("key", "value");
		var found = testee.TryGetValue("key", out var value);

		await Assert.That(found).IsTrue();
		await Assert.That(value).IsEqualTo("value");
	}

	[Test]
	public async Task TypedMemoryCache_WhenRemoveCalled_KeyIsNoLongerFound()
	{
		var services = new ServiceCollection();
		services.AddModule<CoreModule>();
		using var serviceProvider = services.BuildServiceProvider();
		using var scope = serviceProvider.CreateScope();
		var testee = scope.ServiceProvider.GetRequiredService<ITypedMemoryCache<string, string>>();

		testee.Set("key", "value");
		testee.Remove("key");
		var found = testee.TryGetValue("key", out _);

		await Assert.That(found).IsFalse();
	}
}
