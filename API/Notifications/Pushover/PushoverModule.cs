using API.Http;
using Contracts.Notifications;
using Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace API.Notifications.Pushover;

internal sealed class PushoverModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddHttpClient<PushoverClient>(Configure).AddProviderHttp<PushoverOptions>();
		services.AddKeyedScoped<INotificationChannel, PushoverChannel>(NotificationChannel.Pushover);
	}

	private static void Configure(IServiceProvider sp, HttpClient client)
	{
		client.BaseAddress = new Uri($"{sp.GetRequiredService<PushoverOptions>().BaseUrl.TrimEnd('/')}/");
	}
}
