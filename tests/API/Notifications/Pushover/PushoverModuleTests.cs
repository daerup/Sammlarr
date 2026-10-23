using System.Net;
using API.Notifications.Pushover;
using Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Shared.Options;

namespace API.Tests.Notifications.Pushover;

public class PushoverModuleTests
{
	[Test]
	public async Task SendAsync_ThroughTheModule_PostsToTheConfiguredBaseUrl()
	{
		Uri? sentTo = null;
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(new PushoverOptions { BaseUrl = "http://catcher.test/pushover/1" });
		new PushoverModule().RegisterServices(services);
		services.AddHttpClient<PushoverClient>().ConfigurePrimaryHttpMessageHandler(() => new AnsweringHandler(request => sentTo = request.RequestUri));
		await using var provider = services.BuildServiceProvider();

		await provider
			.GetRequiredService<PushoverClient>()
			.SendAsync(new PushoverMessage(new Secret("app-token"), new Secret("user-key"), "Title", "Message", PushoverPriority.Normal), CancellationToken.None);

		await Assert.That(sentTo?.ToString()).IsEqualTo("http://catcher.test/pushover/1/messages.json");
	}

	private sealed class AnsweringHandler(Action<HttpRequestMessage> seen) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			seen(request);
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
		}
	}
}
