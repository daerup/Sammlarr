using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Shared.Options;

public record OptionsModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddGeneratedOptions();
	}
}
