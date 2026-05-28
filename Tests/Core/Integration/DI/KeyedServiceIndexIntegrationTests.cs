using Contracts.DI;
using Core;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Core.Tests.Integration.DI;

public class KeyedServiceIndexIntegrationTests
{
	[Test]
	public async Task KeyedServiceIndex_WhenServiceIsRegistered_ReturnsServiceForKey()
	{
		var services = new ServiceCollection();
		services.AddModule<CoreModule>();
		services.AddKeyedScoped<ITestService, TmdbService>(TestProvider.Tmdb);
		services.AddKeyedScoped<ITestService, PlexService>(TestProvider.Plex);
		using var serviceProvider = services.BuildServiceProvider();
		using var scope = serviceProvider.CreateScope();
		var testee = scope.ServiceProvider.GetRequiredService<IKeyedServiceIndex<TestProvider, ITestService>>();

		var tmdbService = testee[TestProvider.Tmdb];
		var plexService = testee[TestProvider.Plex];

		await Assert.That(tmdbService.Name).IsEqualTo("tmdb");
		await Assert.That(plexService.Name).IsEqualTo("plex");
	}

	[Test]
	public async Task KeyedServiceIndex_WhenServiceIsMissingForKey_ThrowsInvalidOperationException()
	{
		var services = new ServiceCollection();
		services.AddModule<CoreModule>();
		services.AddKeyedScoped<ITestService, TmdbService>(TestProvider.Tmdb);
		using var serviceProvider = services.BuildServiceProvider();
		using var scope = serviceProvider.CreateScope();
		var testee = scope.ServiceProvider.GetRequiredService<IKeyedServiceIndex<TestProvider, ITestService>>();

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
