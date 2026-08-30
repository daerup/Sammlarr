using API.Http;
using Contracts.Notifications;
using Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.Notifications;

internal sealed class NotificationsApiModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<INotificationTemplateRenderer, FluidTemplateRenderer>();

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

		services.AddHttpClient<PushoverClient>().AddProviderHttp<PushoverOptions>();
		services.AddKeyedScoped<INotificationChannel, PushoverChannel>(NotificationChannel.Pushover);
	}
}
