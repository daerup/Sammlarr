using Contracts.Providers;
using Contracts.Usage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polly.Telemetry;
using Shared.DI;

namespace API.Http;

internal class HttpModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.TryAddSingleton(TimeProvider.System);
		services.AddSingleton<Lanes.Lanes>();
		services.AddHostedService<Lanes.LaneSettingsCheck>();
		services.AddSingleton<RemoteCapacity>();
		services.AddSingleton<IRemoteCapacity>(sp => sp.GetRequiredService<RemoteCapacity>());
		services.AddOptions<TelemetryOptions>().Configure<IFeedRunUsage>((options, feedRuns) => options.MeteringEnrichers.Add(new UsageMeteringEnricher(feedRuns)));
	}
}
