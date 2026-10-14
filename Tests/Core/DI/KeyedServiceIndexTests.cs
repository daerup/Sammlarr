using Core.DI;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Tests.DI;

public class KeyedServiceIndexTests
{
	[Test]
	public async Task KeyedServiceIndex_WhenServiceIsRegistered_ReturnsServiceForKey()
	{
		var services = new ServiceCollection();
		services.AddKeyedScoped<ITestService, TmdbService>(TestProvider.Tmdb);
		services.AddKeyedScoped<ITestService, PlexService>(TestProvider.Plex);
		await using var serviceProvider = services.BuildServiceProvider();
		using var scope = serviceProvider.CreateScope();
		var testee = new KeyedServiceIndex<TestProvider, ITestService>(scope.ServiceProvider);

		var tmdbService = testee[TestProvider.Tmdb];
		var plexService = testee[TestProvider.Plex];

		await Assert.That(tmdbService.Name).IsEqualTo("tmdb");
		await Assert.That(plexService.Name).IsEqualTo("plex");
	}

	[Test]
	public async Task KeyedServiceIndex_WhenServiceIsMissingForKey_ThrowsInvalidOperationException()
	{
		var services = new ServiceCollection();
		services.AddKeyedScoped<ITestService, TmdbService>(TestProvider.Tmdb);
		await using var serviceProvider = services.BuildServiceProvider();
		using var scope = serviceProvider.CreateScope();
		var testee = new KeyedServiceIndex<TestProvider, ITestService>(scope.ServiceProvider);

		await Assert.That(() => testee[TestProvider.Plex]).Throws<InvalidOperationException>();
	}

	private enum TestProvider
	{
		Tmdb,
		Plex,
	}

	private interface ITestService
	{
		string Name { get; }
	}

	private class TmdbService : ITestService
	{
		public string Name => "tmdb";
	}

	private class PlexService : ITestService
	{
		public string Name => "plex";
	}
}
