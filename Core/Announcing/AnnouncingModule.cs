using Contracts.Announcing;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Core.Announcing;

internal sealed class AnnouncingModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<IEventPublisher, EventPublisher>();
		services.AddScoped<Announcer>();
	}
}
