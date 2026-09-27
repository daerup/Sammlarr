using Contracts.Announcing;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Core.Announcing;

internal class AnnouncingModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<IEventPublisher, EventPublisher>();
		services.AddScoped<Announcer>();
	}
}
