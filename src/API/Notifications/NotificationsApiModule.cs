using API.Notifications.Pushover;
using API.Notifications.Webhook;
using Contracts.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace API.Notifications;

internal sealed class NotificationsApiModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<INotificationTemplateRenderer, FluidTemplateRenderer>();
		services.AddModule<WebhookModule>();
		services.AddModule<PushoverModule>();
	}
}
