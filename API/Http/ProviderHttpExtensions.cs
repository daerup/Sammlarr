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
	public static IHttpClientBuilder AddProviderHttp<TOptions>(this IHttpClientBuilder builder)
		where TOptions : class, IProviderHttpOptions
	{
		return AddPipeline<TOptions>(builder, new None());
	}

	public static IHttpClientBuilder AddProviderHttp<TOptions>(this IHttpClientBuilder builder, MediaProvider provider)
		where TOptions : class, IProviderHttpOptions
	{
		return AddPipeline<TOptions>(builder, Some.Of(provider));
	}

	private static IHttpClientBuilder AddPipeline<TOptions>(IHttpClientBuilder builder, Optional<MediaProvider> provider)
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
			var gate = Optional.When(http.MaxConcurrency > 0, () => sp.GetRequiredService<ProviderConcurrencyGates>().Get(clientName, http.MaxConcurrency, provider));
			return new ConcurrencyGateHandler(gate);
		});

		builder.ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler { EnableMultipleHttp2Connections = Resolve(sp).EnableMultipleHttp2Connections });

		builder.AddStandardResilienceHandler().Configure((options, sp) => ConfigureResilience(options, Resolve(sp), provider, sp));

		provider
			.Apply(measured => builder.AddHttpMessageHandler(sp => new CapacityHandler(measured, sp.GetRequiredService<ProviderCapacity>())))
			.Apply(measured => builder.Services.AddSingleton(sp => Wait(measured, Resolve(sp))));

		return builder;
	}

	private static ProviderWait Wait(MediaProvider provider, ProviderHttpOptions http)
	{
		return new ProviderWait(provider, TimeSpan.FromSeconds(http.MaxWaitSeconds), TimeSpan.FromSeconds(http.EnrichWaitSeconds));
	}

	private static void ConfigureResilience(
		HttpStandardResilienceOptions options,
		ProviderHttpOptions http,
		Optional<MediaProvider> provider,
		IServiceProvider services
	)
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

		options.CircuitBreaker.FailureRatio = http.CircuitBreakerFailureRatio;
		options.CircuitBreaker.MinimumThroughput = http.CircuitBreakerMinimumThroughput;
		provider.Apply(limited => ReportRateLimits(options, limited, services)).Apply(broken => BlockWhileOpen(options, broken, services));
	}

	private static void ReportRateLimits(HttpStandardResilienceOptions options, MediaProvider provider, IServiceProvider services)
	{
		var reporter = services.GetRequiredService<IProviderRateLimitReporter>();
		options.Retry.OnRetry = async args =>
		{
			if (args.Outcome.Result?.StatusCode is HttpStatusCode.TooManyRequests)
			{
				await args
					.Outcome.Result.TriggeredBy()
					.Apply(triggeredBy => reporter.ReportAsync(provider, args.RetryDelay, triggeredBy, args.Context.CancellationToken));
			}
		};
	}

	private static void BlockWhileOpen(HttpStandardResilienceOptions options, MediaProvider provider, IServiceProvider services)
	{
		var capacity = services.GetRequiredService<ProviderCapacity>();
		options.CircuitBreaker.OnOpened = args =>
		{
			capacity.Block(provider, args.BreakDuration);
			return default;
		};
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
	private readonly ConcurrentDictionary<string, (SemaphoreSlim Gate, int Limit, Optional<MediaProvider> Provider)> _gates = new();

	public SemaphoreSlim Get(string key, int limit, Optional<MediaProvider> provider)
	{
		return _gates.GetOrAdd(key, static (_, gate) => (new SemaphoreSlim(gate.limit, gate.limit), gate.limit, gate.provider), (limit, provider)).Gate;
	}

	public double Busy(MediaProvider provider)
	{
		return _gates
			.Where(pair => pair.Value.Provider.Match(owner => owner == provider, () => false))
			.Select(pair => (pair.Value.Limit - pair.Value.Gate.CurrentCount) / (double)pair.Value.Limit)
			.DefaultIfEmpty(0)
			.Max();
	}
}

internal sealed class ConcurrencyGateHandler(Optional<SemaphoreSlim> gate) : DelegatingHandler
{
	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		return gate.Match(held => SendThroughAsync(held, request, cancellationToken), () => base.SendAsync(request, cancellationToken));
	}

	private async Task<HttpResponseMessage> SendThroughAsync(SemaphoreSlim held, HttpRequestMessage request, CancellationToken cancellationToken)
	{
		await held.WaitAsync(cancellationToken);
		try
		{
			return await base.SendAsync(request, cancellationToken);
		}
		finally
		{
			held.Release();
		}
	}
}
