using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shared.DI;
using UI.Telemetry.Diagnostics;

namespace UI.Telemetry;

internal class TelemetryModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<LogCaptureService>();
		services.AddSingleton<InMemoryActivityProcessor>();
		services.AddSingleton<ILoggerProvider, InMemoryLoggerProvider>();
		services.AddOptions<LoggerFilterOptions>().Configure(opts => opts.Rules.Add(new LoggerFilterRule("InMemoryDiagnostics", null, LogLevel.Trace, null)));

		services
			.AddOpenTelemetry()
			.WithTracing(tracing => tracing.AddAspNetCoreInstrumentation().AddProcessor(sp => sp.GetRequiredService<InMemoryActivityProcessor>()))
			.WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation());

		services.Configure<HubOptions>(o => o.AddFilter(new BlazorHubActivityFilter()));
	}
}
