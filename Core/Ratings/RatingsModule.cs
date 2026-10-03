using Contracts.Ratings;
using Microsoft.Extensions.DependencyInjection;
using Observability.Tracing;
using Shared.DI;

namespace Core.Ratings;

internal class RatingsModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddTracedScoped<IRatingService, RatingService>();
	}
}
