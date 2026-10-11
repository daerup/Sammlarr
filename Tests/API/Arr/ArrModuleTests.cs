using API.Arr;
using API.Http;
using Contracts;
using Contracts.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.DI;
using TestInfrastructure;

namespace API.Tests.Arr;

public class ArrModuleTests
{
	[Test]
	public async Task StartAsync_WithTheShippedArrSettings_Starts()
	{
		using var host = Host();

		await host.StartAsync();
		await host.StopAsync();
	}

	[Test]
	[Arguments("Arr:Radarr:MinimumAvailability", "Soon")]
	[Arguments("Arr:Sonarr:SeriesType", "Weekly")]
	[Arguments("Arr:Sonarr:SeasonFolder", "sometimes")]
	[Arguments("Arr:Radarr:Http:AttemptTimeoutSeconds", "forever")]
	public async Task StartAsync_WithAnArrSettingThatCannotBeBoundOrFailsValidation_RefusesToStart(string key, string value)
	{
		using var host = Host(new KeyValuePair<string, string?>(key, value));

		await Assert.That(async () => await host.StartAsync()).ThrowsException();
	}

	private static IHost Host(params KeyValuePair<string, string?>[] overrides)
	{
		return new HostBuilder()
			.ConfigureAppConfiguration(configuration => configuration.AddJsonFile(RepositoryRoot.Resolve("Web", "appsettings.json")).AddInMemoryCollection(overrides))
			.ConfigureServices(services =>
			{
				services.AddModule<SharedModule>();
				services.AddModule<HttpModule>();
				services.AddSingleton(IDateTimeProvider.Mock().Object);
				services.AddSingleton(IProviderRateLimitReporter.Mock().Object);
				services.AddModule<ArrModule>();
			})
			.Build();
	}
}
