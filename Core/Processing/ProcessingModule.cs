using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Core.Processing;

internal class ProcessingModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddTransient(typeof(QueryProcessor<>));
	}
}
