using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Shared.DI;
using Shared.Options;

namespace Observability.Logging;

internal sealed class LoggingModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		var context = ObservabilityContext.Resolve();

		services.AddSingleton(provider => new LoggingLevelSwitch(ObservabilityContext.MapLevel(provider.GetRequiredService<ObservabilityOptions>().LogLevel)));
		services.AddLogging();
		services.AddSerilog((provider, logger) => LogPipeline.Configure(logger, provider, context));
	}
}
