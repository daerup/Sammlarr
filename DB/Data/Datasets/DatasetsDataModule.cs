using Contracts.Datasets;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Datasets;

internal sealed class DatasetsDataModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IImdbRatings, ImdbRatingRepository>();
		services.AddScoped<IDatasetTable<ImdbRatingLine>>(provider => provider.GetRequiredService<IImdbRatings>());
		services.AddScoped<IAnimeIds, AnimeIdRepository>();
		services.AddScoped<IDatasetTable<AnimeIdMapping>>(provider => provider.GetRequiredService<IAnimeIds>());
		services.AddScoped<IDatasetLoads, DatasetLoadRepository>();
		services.AddScoped<IDatasetChecks, DatasetCheckRepository>();
		services.AddScoped<IModelConfiguration, DatasetsConfiguration>();
	}
}
