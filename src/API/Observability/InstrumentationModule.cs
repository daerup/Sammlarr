using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Shared.DI;

namespace API.Observability;

public class InstrumentationModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddOpenTelemetry().WithTracing(tracing => tracing.AddHttpClientInstrumentation());
	}
}
