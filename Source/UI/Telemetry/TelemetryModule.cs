using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shared.DI;

namespace UI.Telemetry;

internal class TelemetryModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services
			.AddOpenTelemetry()
			.WithTracing(tracing => tracing.AddAspNetCoreInstrumentation())
			.WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation());

		services.Configure<HubOptions>(o => o.AddFilter(new BlazorHubActivityFilter()));
	}
}
