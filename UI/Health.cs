using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shared.DI;

namespace UI;

internal sealed class HealthModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services)
	{
		services.AddRequestTimeouts(timeouts => timeouts.AddPolicy(HealthEndpoints.Policy, TimeSpan.FromSeconds(5)));
		services.AddOutputCache(caching => caching.AddPolicy(HealthEndpoints.Policy, policy => policy.Expire(TimeSpan.FromSeconds(10))));
		services.AddHealthChecks().AddCheck("self", static () => HealthCheckResult.Healthy(), ["live"]);
	}
}

internal static class HealthEndpoints
{
	internal const string Policy = "HealthChecks";
	private const string HealthEndpointPath = "/health";
	private const string AlivenessEndpointPath = "/alive";

	public static WebApplication MapHealthEndpoints(this WebApplication app)
	{
		var healthChecks = app.MapGroup("");
		healthChecks.CacheOutput(Policy).WithRequestTimeout(Policy);
		healthChecks.MapHealthChecks(HealthEndpointPath);
		healthChecks.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions { Predicate = static check => check.Tags.Contains("live") });
		return app;
	}
}
