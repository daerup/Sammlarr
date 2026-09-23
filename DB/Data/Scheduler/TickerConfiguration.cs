using Microsoft.EntityFrameworkCore;
using TickerQ.EntityFrameworkCore.Configurations;
using TickerQ.Utilities.Entities;

namespace DB.Data.Scheduler;

internal sealed class TickerConfiguration : IModelConfiguration
{
	public void Apply(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfiguration(new TimeTickerConfigurations<TimeTickerEntity>(schema: null!));
		modelBuilder.ApplyConfiguration(new CronTickerConfigurations<CronTickerEntity>(schema: null!));
		modelBuilder.ApplyConfiguration(new CronTickerOccurrenceConfigurations<CronTickerEntity>(schema: null!));
	}
}
