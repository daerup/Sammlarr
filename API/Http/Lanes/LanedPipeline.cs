using System.Net;
using Contracts.Providers;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;
using Shared.Options;

namespace API.Http.Lanes;

internal static class LanedPipeline
{
	internal const string RetryPipeline = "retry";

	public static IHttpClientBuilder AddLanes<TOptions>(this IHttpClientBuilder builder, RemoteService remote)
		where TOptions : class, IProviderHttpOptions
	{
		builder.AddResilienceHandler(
			RetryPipeline,
			(pipeline, context) =>
				pipeline.AddRetry(Retry(remote, context.ServiceProvider.GetRequiredService<TOptions>().Http, context.ServiceProvider.GetRequiredService<Lanes>()))
		);
		builder.AddHttpMessageHandler(services => new LaneHandler(
			remote,
			services.GetRequiredService<Lanes>(),
			services.GetRequiredService<IProviderRateLimitReporter>(),
			services.GetRequiredService<TimeProvider>()
		));
		builder.AddResilienceHandler(
			"attempt",
			(pipeline, context) => pipeline.AddTimeout(TimeSpan.FromSeconds(context.ServiceProvider.GetRequiredService<TOptions>().Http.AttemptTimeoutSeconds))
		);
		return builder;
	}

	private static HttpRetryStrategyOptions Retry(RemoteService remote, ProviderHttpOptions http, Lanes lanes)
	{
		return new HttpRetryStrategyOptions
		{
			MaxRetryAttempts = Math.Max(1, http.RetryAttempts),
			BackoffType = DelayBackoffType.Exponential,
			Delay = TimeSpan.FromMilliseconds(500),
			UseJitter = true,
			ShouldHandle = args =>
				ValueTask.FromResult(
					http.RetryAttempts > 0
						&& args.Outcome.Exception is not TimeoutRejectedException
						&& HttpClientResiliencePredicates.IsTransient(args.Outcome)
						&& Optional.Of(args.Context.GetRequestMessage()).Match(request => lanes.Admits(remote, request), () => true)
				),
			DelayGenerator = args => ValueTask.FromResult<TimeSpan?>(args.Outcome.Result?.StatusCode is HttpStatusCode.TooManyRequests ? TimeSpan.Zero : null),
			OnRetry = args =>
			{
				args.Context.Properties.Set(UsageStamp.Retried, args.Context.Properties.GetValue(UsageStamp.Retried, 0) + 1);
				if (args.Outcome.Result?.StatusCode is HttpStatusCode.TooManyRequests)
				{
					args.Context.Properties.Set(UsageStamp.RateLimited, args.Context.Properties.GetValue(UsageStamp.RateLimited, 0) + 1);
				}

				return default;
			},
		};
	}
}
