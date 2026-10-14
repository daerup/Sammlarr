using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Shared.DI;

namespace Observability.Diagnostics;

internal sealed class DiagnosticsModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<LogCaptureService>();
		services.AddSingleton<InMemoryActivityProcessor>();
		services.AddSingleton<ILogEventSink, InMemorySink>();
	}
}
