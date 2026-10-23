using Domain.Notifications;
using TestInfrastructure;

namespace Domain.Tests.Notifications;

public class WebhookConfigTests
{
	[Test]
	[Arguments("http://catcher:8080/hook")]
	[Arguments("https://example.test/hook")]
	public async Task Endpoint_ForAnHttpOrHttpsUrl_IsTheUrl(string url)
	{
		await Assert.That(new WebhookConfig(new Secret(url), new Secret("whsec_secret")).Endpoint()).IsSome().And.IsEqualTo(new Uri(url));
	}

	[Test]
	[Arguments("ftp://example.test/hook")]
	[Arguments("/hook")]
	[Arguments("")]
	public async Task Endpoint_ForAnythingElse_IsNone(string url)
	{
		await Assert.That(new WebhookConfig(new Secret(url), new Secret("whsec_secret")).Endpoint()).IsNone();
	}
}
