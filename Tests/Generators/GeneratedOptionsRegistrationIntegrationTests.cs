using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Options;

namespace Generators.Tests;

public class GeneratedOptionsRegistrationIntegrationTests
{
	[Test]
	public async Task AddGeneratedOptions_ResolvesOptionsMonitor()
	{
		var provider = BuildProvider();
		var monitor = provider.GetRequiredService<IOptionsMonitor<DummyOptions>>();

		await Assert.That(monitor.CurrentValue.BaseUrl).IsEqualTo("https://example.test");
	}

	[Test]
	public async Task AddGeneratedOptions_ResolvesDirectOptions()
	{
		var provider = BuildProvider();
		var resolved = provider.GetRequiredService<DummyOptions>();

		await Assert.That(resolved.BaseUrl).IsEqualTo("https://example.test");
		await Assert.That(resolved.Enabled).IsTrue();
	}

	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		var configCollection = new Dictionary<string, string?>
		{
			[$"{DummyOptions.SectionName}:BaseUrl"] = "https://example.test",
			[$"{DummyOptions.SectionName}:Enabled"] = "true",
		};
		var config = new ConfigurationBuilder().AddInMemoryCollection(configCollection).Build();
		services.AddSingleton<IConfiguration>(config);
		services.AddGeneratedOptions();

		return services.BuildServiceProvider();
	}
}
