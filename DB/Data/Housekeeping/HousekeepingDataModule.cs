using Contracts.Housekeeping;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Housekeeping;

internal class HousekeepingDataModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IDatabaseFile, DatabaseFile>();
		services.AddScoped<IHousekeepingReports, HousekeepingReportRepository>();
		services.AddScoped<IModelConfiguration, HousekeepingReportConfiguration>();
	}
}
