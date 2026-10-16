using Contracts.Providers;
using Contracts.Usage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polly.Telemetry;
using Shared.DI;

namespace API.Http;

internal sealed class HttpModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.TryAddSingleton(TimeProvider.System);
		services.AddSingleton<Lanes.Lanes>();
		services.AddHostedService<Lanes.LaneSettingsCheck>();
		services.AddScoped<IRemoteCapacity, RemoteCapacity>();
		services.AddOptions<TelemetryOptions>().Configure<IFeedRunUsage>((options, feedRuns) => options.MeteringEnrichers.Add(new UsageMeteringEnricher(feedRuns)));
	}
}
