using API.Http;
using API.Notifications.Webhook;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Options;

namespace API.Tests.Http;

public class ProviderHttpExtensionsTests
{
	[Test]
	public async Task AddProviderHttp_WithZeroRetries_PassesStartupOptionsValidation()
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
