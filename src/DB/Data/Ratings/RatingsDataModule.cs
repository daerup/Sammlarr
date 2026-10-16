using Contracts.Ratings;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Ratings;

internal sealed class RatingsDataModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IRatingDefaultRepository, RatingDefaultRepository>();
		services.AddScoped<IModelConfiguration, RatingDefaultConfiguration>();
	}
}
