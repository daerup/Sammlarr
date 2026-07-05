using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly.Timeout;
using Shared.DI;

namespace API;

internal sealed class ResilienceModule : IServiceModule
{
	public void RegisterServices(IServiceCollection services) => services.ConfigureHttpClientDefaults(builder => builder.AddStandardResilienceHandler(Configure));

	private static void Configure(HttpStandardResilienceOptions options)
	{
		options.Retry.MaxRetryAttempts = 2;
		options.Retry.UseJitter = true;
		options.Retry.ShouldHandle = static args => new ValueTask<bool>(
			args.Outcome.Exception is not TimeoutRejectedException && HttpClientResiliencePredicates.IsTransient(args.Outcome)
		);
		options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(6);
		options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
		options.CircuitBreaker.FailureRatio = 0.5;
		options.CircuitBreaker.MinimumThroughput = 20;
	}
}
