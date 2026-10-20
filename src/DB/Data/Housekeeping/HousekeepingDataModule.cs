using Contracts.Housekeeping;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace DB.Data.Housekeeping;

internal sealed class HousekeepingDataModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddScoped<IDatabaseFile, DatabaseFile>();
		services.AddScoped<IHousekeepingReports, HousekeepingReportRepository>();
		services.AddScoped<IModelConfiguration, HousekeepingReportConfiguration>();
		services.AddScoped<IHousekeepingStep, RunHistoryTrim>();
		services.AddScoped<IHousekeepingStep, CachedDetailsTrim>();
		services.AddScoped<IHousekeepingStep, TitleIdsTrim>();
		services.AddScoped<IHousekeepingStep, ShowPartsTrim>();
		services.AddScoped<IHousekeepingStep, SettledJobsTrim>();
		services.AddScoped<IHousekeepingStep, ScheduleEntriesTrim>();
	}
}
