using Microsoft.Extensions.DependencyInjection;
using Observability.Diagnostics;
using Observability.Logging;
using Observability.Tracing;
using Shared.DI;

namespace Observability;

public class ObservabilityModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddModule<DiagnosticsModule>();
		services.AddModule<LoggingModule>();
		services.AddModule<TracingModule>();
	}
}
