using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Shared.DI;

namespace DB.Observability;

public class InstrumentationModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services
			.AddOpenTelemetry()
			.WithTracing(tracing =>
				tracing.AddEntityFrameworkCoreInstrumentation(opts =>
				{
					opts.EnrichWithIDbCommand = (activity, command) =>
					{
						var (operation, table) = SqlSummary.Parse(command.CommandText);
						if (operation is not null)
						{
							activity.SetTag("db.operation", operation);
						}
						if (table is not null)
						{
							activity.SetTag("db.collection.name", table);
						}
						if (operation is not null && table is not null)
						{
							activity.DisplayName = $"{operation} {table}";
						}
					};
				})
			);
	}
}
