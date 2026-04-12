using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace API.IMDB;

internal class ImdbModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<ImdbIdConverter>();
	}
}
