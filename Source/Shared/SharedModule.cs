using Microsoft.Extensions.DependencyInjection;
using Shared.DI;
using Shared.Options;

namespace Shared;

public class SharedModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<OptionsModule>();
	}
}
