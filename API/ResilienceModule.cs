using API.Http;
using Microsoft.Extensions.DependencyInjection;
using Polly.Telemetry;
using Shared.DI;

namespace API;

internal sealed class ResilienceModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddSingleton<ProviderConcurrencyGates>();
		services.Configure<TelemetryOptions>(options => options.SeverityProvider = args => Severity(args.Event.EventName, args.Event.Severity));
	}

	internal static ResilienceEventSeverity Severity(string eventName, ResilienceEventSeverity defaultSeverity) =>
		eventName switch
		{
			"ExecutionAttempt" => defaultSeverity >= ResilienceEventSeverity.Warning ? ResilienceEventSeverity.Warning : ResilienceEventSeverity.None,
			"OnRetry" or "OnTimeout" or "OnHedging" => ResilienceEventSeverity.Warning,
			_ => defaultSeverity,
		};
}
