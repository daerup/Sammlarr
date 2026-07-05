using Microsoft.Extensions.DependencyInjection;
using Observability.Diagnostics;
using Observability.Logging;
using Observability.Tracing;
using Serilog;
using Serilog.Core;
using Shared.DI;
using Shared.Options;

namespace Observability;

public class ObservabilityModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		var context = ObservabilityContext.Resolve();

		services.AddSingleton(provider => new LoggingLevelSwitch(ObservabilityContext.MapLevel(provider.GetRequiredService<ObservabilityOptions>().LogLevel)));
		services.AddSingleton<LogCaptureService>();
		services.AddSingleton<InMemoryActivityProcessor>();
		services.AddSingleton<ILogEventSink, InMemorySink>();

		services.AddLogging();
		services.AddSerilog((provider, logger) => LogPipeline.Configure(logger, provider, context));

		TracingPipeline.Register(services, context);
	}
}
