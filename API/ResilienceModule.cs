using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Shared.DI;

namespace API;

internal sealed class ResilienceModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services) => services.ConfigureHttpClientDefaults(builder => builder.AddStandardResilienceHandler(Configure));

	private static void Configure(HttpStandardResilienceOptions options)
	{
		options.Retry.MaxRetryAttempts = 5;
		options.Retry.UseJitter = true;
		options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
		options.CircuitBreaker.FailureRatio = 0.9;
		options.CircuitBreaker.MinimumThroughput = 1000;
		options.RateLimiter = new HttpRateLimiterStrategyOptions
		{
			DefaultRateLimiterOptions = new ConcurrencyLimiterOptions { PermitLimit = 8, QueueLimit = 4096 },
		};
	}
}
