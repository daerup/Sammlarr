using Contracts.Datasets;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Datasets;

internal class DatasetsDataModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IImdbRatings, ImdbRatingRepository>();
		services.AddScoped<IDatasetTable<ImdbRatingLine>>(provider => provider.GetRequiredService<IImdbRatings>());
		services.AddScoped<IDatasetLoads, DatasetLoadRepository>();
		services.AddScoped<IModelConfiguration, DatasetsConfiguration>();
	}
}
