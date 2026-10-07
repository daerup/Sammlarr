using Contracts.Datasets;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Core.Datasets;

internal sealed class DatasetsModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IDatasetLoader, DatasetLoader<ImdbRatingLine>>();
		services.AddHostedService<DatasetSchedule>();
	}
}
