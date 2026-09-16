using API.Http;
using Contracts.Notifications;
using Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.Notifications.Webhook;

internal sealed class WebhookModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services
			.AddHttpClient<WebhookClient>()
			.AddProviderHttp<WebhookOptions>()
			.ConfigurePrimaryHttpMessageHandler(() =>
				new SocketsHttpHandler
				{
					EnableMultipleHttp2Connections = true,
					AllowAutoRedirect = false,
					ConnectCallback = WebhookConnect.ConnectAsync,
				}
			);

		services.AddKeyedScoped<INotificationChannel, WebhookChannel>(NotificationChannel.Webhook);
	}
}
