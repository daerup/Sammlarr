using API.Http;
using API.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Options;

namespace API.Tests.Http;

public class ProviderHttpExtensionsTests
{
	[Test]
	public async Task Zero_retry_provider_http_passes_startup_options_validation()
	{
		using var host = new HostBuilder()
			.ConfigureServices(services =>
			{
				services.AddSingleton(new WebhookOptions());
				services.AddSingleton<ProviderConcurrencyGates>();
				services.AddHttpClient<WebhookClient>().AddProviderHttp<WebhookOptions>();
			})
			.Build();

		await host.StartAsync();
		await host.StopAsync();
	}
}
