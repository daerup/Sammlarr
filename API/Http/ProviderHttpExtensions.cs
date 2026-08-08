using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly.Timeout;
using Shared.Options;

namespace API.Http;

internal static class ProviderHttpExtensions
{
	public static IHttpClientBuilder AddProviderHttp<TOptions>(this IHttpClientBuilder builder)
		where TOptions : class, IProviderHttpOptions
	{
		var clientName = builder.Name;
		ProviderHttpOptions Resolve(IServiceProvider sp) => sp.GetRequiredService<TOptions>().Http;

		builder.ConfigureHttpClient(
			(sp, client) =>
			{
				var http = Resolve(sp);
				client.DefaultRequestVersion = ParseVersion(http.HttpVersion);
				client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
			}
		);

		builder.AddHttpMessageHandler(sp =>
		{
			var http = Resolve(sp);
			var gate = http.MaxConcurrency > 0 ? sp.GetRequiredService<ProviderConcurrencyGates>().Get(clientName, http.MaxConcurrency) : null;
			return new ConcurrencyGateHandler(gate);
		});

		builder.ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler { EnableMultipleHttp2Connections = Resolve(sp).EnableMultipleHttp2Connections });

		builder.AddStandardResilienceHandler().Configure((options, sp) => Apply(options, Resolve(sp)));

		return builder;
	}

	private static void Apply(HttpStandardResilienceOptions options, ProviderHttpOptions http)
	{
		options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(http.AttemptTimeoutSeconds);
		options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(http.TotalTimeoutSeconds);
		options.Retry.MaxRetryAttempts = http.RetryAttempts;
		options.Retry.UseJitter = true;
		options.Retry.ShouldHandle = static args => new ValueTask<bool>(
			args.Outcome.Exception is not TimeoutRejectedException && HttpClientResiliencePredicates.IsTransient(args.Outcome)
		);
		if (http.HonorRetryAfter)
		{
			options.Retry.DelayGenerator = static args =>
			{
				var retryAfter = args.Outcome.Result?.Headers.RetryAfter;
				var delay = retryAfter?.Delta ?? retryAfter?.Date - DateTimeOffset.UtcNow;
				return new ValueTask<TimeSpan?>(delay is { } value && value > TimeSpan.Zero ? value : null);
			};
		}
		options.CircuitBreaker.FailureRatio = http.CircuitBreakerFailureRatio;
		options.CircuitBreaker.MinimumThroughput = http.CircuitBreakerMinimumThroughput;
	}

	private static Version ParseVersion(string version) =>
		version.Trim() switch
		{
			"1.0" => HttpVersion.Version10,
			"1.1" => HttpVersion.Version11,
			"3" or "3.0" => HttpVersion.Version30,
			_ => HttpVersion.Version20,
		};
}

internal sealed class ProviderConcurrencyGates
{
	private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();

	public SemaphoreSlim Get(string key, int limit) => _gates.GetOrAdd(key, static (_, max) => new SemaphoreSlim(max, max), limit);
}

internal sealed class ConcurrencyGateHandler(SemaphoreSlim? gate) : DelegatingHandler
{
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		if (gate is null)
		{
			return await base.SendAsync(request, cancellationToken);
		}

		await gate.WaitAsync(cancellationToken);
		try
		{
			return await base.SendAsync(request, cancellationToken);
		}
		finally
		{
			gate.Release();
		}
	}
}
