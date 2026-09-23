using Microsoft.Extensions.DependencyInjection;
using Shared.DI;

namespace Observability.Tracing;

internal class TracingModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services) => TracingPipeline.Register(services, ObservabilityContext.Resolve());
}
