using API.Http;
using Contracts.Notifications;
using Domain.Identity;
using Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.Notifications.Webhook;

internal sealed class WebhookModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		foreach (var role in Enum.GetValues<UserRole>())
		{
			services
				.AddHttpClient(WebhookClient.Name(role))
				.RemoveAllLoggers()
				.AddClientHttp<WebhookOptions>()
				.ConfigurePrimaryHttpMessageHandler(() =>
					new SocketsHttpHandler
					{
						EnableMultipleHttp2Connections = true,
						AllowAutoRedirect = false,
						ConnectCallback = (context, cancellationToken) => WebhookConnect.ConnectAsync(context, role, cancellationToken),
					}
				);
		}

		services.AddScoped<WebhookClient>();
		services.AddKeyedScoped<INotificationChannel, WebhookChannel>(NotificationChannel.Webhook);
	}
}
