using Contracts;
using Contracts.Processing;
using Core.Caching;
using Core.DI;
using Core.Processing;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Core;

public class CoreModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<CachingModule>();
		services.AddModule<DiModule>();
		services.AddTransient<IDateTimeProvider, SystemDateTimeProvider>();
		services.AddTransient(typeof(IQueryProcessor<>), typeof(QueryProcessor<>));
	}
}
