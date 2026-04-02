using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Shared.Options;

internal record OptionsModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddGeneratedOptions();
	}
}
