using Contracts.Providers;
using Contracts.Usage;
using Microsoft.Extensions.DependencyInjection;
using Polly.Telemetry;
using Shared.DI;

namespace API.Http;

internal class HttpModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<ProviderGates>();
		services.AddSingleton<RemoteCapacity>();
		services.AddSingleton<IRemoteCapacity>(sp => sp.GetRequiredService<RemoteCapacity>());
		services.AddOptions<TelemetryOptions>().Configure<IFeedRunUsage>((options, feedRuns) => options.MeteringEnrichers.Add(new UsageMeteringEnricher(feedRuns)));
	}
}
