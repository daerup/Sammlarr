using Contracts.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Notifications;

internal sealed class NotificationDataModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<INotificationTargetRepository, NotificationTargetRepository>();
		services.AddScoped<INotificationSubscriptionRepository, NotificationSubscriptionRepository>();
		services.AddScoped<INotificationDeliveryOutcomeRepository, NotificationDeliveryOutcomeRepository>();
		services.AddScoped<IModelConfiguration, NotificationModelConfiguration>();
	}
}
