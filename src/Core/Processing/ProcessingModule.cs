using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Core.Processing;

internal sealed class ProcessingModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddTransient(typeof(QueryProcessor<>));
	}
}
