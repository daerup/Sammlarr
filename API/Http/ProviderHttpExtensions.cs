using System.Collections.Concurrent;
using System.Net;
using Contracts.Providers;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly.Timeout;
using Shared.Options;

namespace API.Http;

internal static class ProviderHttpExtensions
{
	public static IHttpClientBuilder AddProviderHttp<TOptions>(this IHttpClientBuilder builder, MediaProvider? provider = null)
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
			var gate = http.MaxConcurrency > 0 ? sp.GetRequiredService<ProviderConcurrencyGates>().Get(clientName, http.MaxConcurrency, provider) : null;
			return new ConcurrencyGateHandler(gate);
		});

		builder.ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler { EnableMultipleHttp2Connections = Resolve(sp).EnableMultipleHttp2Connections });

		builder.AddStandardResilienceHandler().Configure((options, sp) => Apply(options, Resolve(sp), provider, sp));

		if (provider is { } measured)
		{
			builder.AddHttpMessageHandler(sp => new CapacityHandler(measured, sp.GetRequiredService<ProviderCapacity>()));
			builder.Services.AddSingleton(sp => new ProviderWait(measured, TimeSpan.FromSeconds(Resolve(sp).MaxWaitSeconds)));
		}

		return builder;
	}

	private static void Apply(HttpStandardResilienceOptions options, ProviderHttpOptions http, MediaProvider? provider, IServiceProvider services)
	{
		options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(http.AttemptTimeoutSeconds);
		options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(http.TotalTimeoutSeconds);
		options.Retry.MaxRetryAttempts = Math.Max(1, http.RetryAttempts);
		options.Retry.UseJitter = true;
		options.Retry.ShouldHandle =
			http.RetryAttempts > 0
				? static args => new ValueTask<bool>(args.Outcome.Exception is not TimeoutRejectedException && HttpClientResiliencePredicates.IsTransient(args.Outcome))
				: static _ => new ValueTask<bool>(false);
		if (http.HonorRetryAfter)
		{
			options.Retry.DelayGenerator = static args =>
			{
				var retryAfter = args.Outcome.Result?.Headers.RetryAfter;
				var delay = retryAfter?.Delta ?? retryAfter?.Date - DateTimeOffset.UtcNow;
				return new ValueTask<TimeSpan?>(delay is { } value && value > TimeSpan.Zero ? value : null);
			};
		}
		if (provider is { } limited)
		{
			var reporter = services.GetRequiredService<IProviderRateLimitReporter>();
			options.Retry.OnRetry = async args =>
			{
				if (args.Outcome.Result?.StatusCode is HttpStatusCode.TooManyRequests && args.Outcome.Result.TriggeredBy() is { } triggeredBy)
				{
					await reporter.ReportAsync(limited, args.RetryDelay, triggeredBy, args.Context.CancellationToken);
				}
			};
		}

		options.CircuitBreaker.FailureRatio = http.CircuitBreakerFailureRatio;
		options.CircuitBreaker.MinimumThroughput = http.CircuitBreakerMinimumThroughput;
		if (provider is { } broken)
		{
			var capacity = services.GetRequiredService<ProviderCapacity>();
			options.CircuitBreaker.OnOpened = args =>
			{
				capacity.Block(broken, args.BreakDuration);
				return default;
			};
		}
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
	private readonly ConcurrentDictionary<string, (SemaphoreSlim Gate, int Limit, MediaProvider? Provider)> _gates = new();

	public SemaphoreSlim Get(string key, int limit, MediaProvider? provider) =>
		_gates.GetOrAdd(key, static (_, gate) => (new SemaphoreSlim(gate.limit, gate.limit), gate.limit, gate.provider), (limit, provider)).Gate;

	public double Busy(MediaProvider provider) =>
		_gates
			.Where(pair => pair.Value.Provider == provider)
			.Select(pair => (pair.Value.Limit - pair.Value.Gate.CurrentCount) / (double)pair.Value.Limit)
			.DefaultIfEmpty(0)
			.Max();
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
