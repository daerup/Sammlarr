using System.Collections.Concurrent;
using System.Net;
using System.Threading.RateLimiting;
using Contracts.Providers;
using Contracts.Usage;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly.Timeout;
using Shared.Options;

namespace API.Http;

internal static class RemoteHttpExtensions
{
	public static IHttpClientBuilder AddClientHttp<TOptions>(this IHttpClientBuilder builder)
		where TOptions : class, IProviderHttpOptions
	{
		return AddPipeline<TOptions>(builder, new None());
	}

	public static IHttpClientBuilder AddRemoteHttp<TOptions>(this IHttpClientBuilder builder, RemoteService remote)
		where TOptions : class, IProviderHttpOptions
	{
		return AddPipeline<TOptions>(builder, Some.Of(remote));
	}

	private static IHttpClientBuilder AddPipeline<TOptions>(IHttpClientBuilder builder, Optional<RemoteService> remote)
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

		remote.Apply(stamped => builder.AddHttpMessageHandler(() => new UsageStampHandler(stamped)));
		builder.AddHttpMessageHandler(sp =>
		{
			var http = Resolve(sp);
			var gate = Optional.When(http.MaxConcurrency > 0, () => sp.GetRequiredService<ProviderGates>().Concurrency(clientName, http.MaxConcurrency, remote));
			return new ConcurrencyGateHandler(gate);
		});

		builder.ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler { EnableMultipleHttp2Connections = Resolve(sp).EnableMultipleHttp2Connections });

		builder.AddStandardResilienceHandler().Configure((options, sp) => ConfigureResilience(options, Resolve(sp), clientName, remote, sp));

		remote
			.Apply(measured => builder.AddHttpMessageHandler(sp => new CapacityHandler(measured, sp.GetRequiredService<RemoteCapacity>())))
			.Apply(_ => builder.AddHttpMessageHandler(sp => new UsageTagsHandler(sp.GetRequiredService<IFeedRunUsage>())))
			.Apply(measured => builder.Services.AddSingleton(sp => Wait(measured, Resolve(sp))));

		return builder;
	}

	private static RemoteWait Wait(RemoteService remote, ProviderHttpOptions http)
	{
		return new RemoteWait(remote, TimeSpan.FromSeconds(http.MaxWaitSeconds), TimeSpan.FromSeconds(http.EnrichWaitSeconds));
	}

	private static void ConfigureResilience(
		HttpStandardResilienceOptions options,
		ProviderHttpOptions http,
		string clientName,
		Optional<RemoteService> remote,
		IServiceProvider services
	)
	{
		var paced = remote.Map(owner => $"{owner}").GetValueOr(clientName);
		Optional
			.When(http.RequestsPerSecond > 0, () => services.GetRequiredService<ProviderGates>().Rate(paced, http.RequestsPerSecond, remote))
			.Apply(limiter => options.RateLimiter.RateLimiter = args => limiter.AcquireAsync(1, args.Context.CancellationToken));
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
		remote.Apply(limited => ReportRateLimits(options, limited, services)).Apply(broken => BlockWhileOpen(options, broken, services));
	}

	private static void ReportRateLimits(HttpStandardResilienceOptions options, RemoteService remote, IServiceProvider services)
	{
		var reporter = services.GetRequiredService<IProviderRateLimitReporter>();
		options.Retry.OnRetry = async args =>
		{
			args.Context.Properties.Set(UsageStamp.Retried, args.Context.Properties.GetValue(UsageStamp.Retried, 0) + 1);
			if (args.Outcome.Result?.StatusCode is HttpStatusCode.TooManyRequests)
			{
				args.Context.Properties.Set(UsageStamp.RateLimited, args.Context.Properties.GetValue(UsageStamp.RateLimited, 0) + 1);
				await args
					.Outcome.Result.TriggeredBy()
					.Apply(triggeredBy => reporter.ReportAsync(remote, args.RetryDelay, triggeredBy, args.Context.CancellationToken));
			}
		};
	}

	private static void BlockWhileOpen(HttpStandardResilienceOptions options, RemoteService remote, IServiceProvider services)
	{
		var capacity = services.GetRequiredService<RemoteCapacity>();
		options.CircuitBreaker.OnOpened = args =>
		{
			capacity.Block(remote, args.BreakDuration);
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

internal sealed class ProviderGates
{
	private readonly ConcurrentDictionary<string, (SemaphoreSlim Gate, int Limit, Optional<RemoteService> Remote)> _gates = new();

	private readonly ConcurrentDictionary<string, (SlidingWindowRateLimiter Window, int Limit, Optional<RemoteService> Remote)> _windows = new();

	public SemaphoreSlim Concurrency(string key, int limit, Optional<RemoteService> remote)
	{
		return _gates.GetOrAdd(key, static (_, gate) => (new SemaphoreSlim(gate.limit, gate.limit), gate.limit, gate.remote), (limit, remote)).Gate;
	}

	public RateLimiter Rate(string key, double perSecond, Optional<RemoteService> remote)
	{
		var permits = Math.Max(1, (int)perSecond);
		return _windows.GetOrAdd(key, static (_, rate) => (Paced(rate.permits, rate.perSecond), rate.permits, rate.remote), (permits, perSecond, remote)).Window;
	}

	public double Busy(RemoteService remote)
	{
		var gates = _gates.Values.Where(gate => Owns(gate.Remote, remote)).Select(gate => (gate.Limit - gate.Gate.CurrentCount) / (double)gate.Limit);
		var windows = _windows.Values.Where(window => Owns(window.Remote, remote)).Select(window => Used(window.Window, window.Limit));
		return gates.Concat(windows).DefaultIfEmpty(0).Max();
	}

	private static bool Owns(Optional<RemoteService> owner, RemoteService remote) => owner.Match(found => found == remote, () => false);

	private static double Used(SlidingWindowRateLimiter window, int limit)
	{
		return Optional
			.Of(window.GetStatistics())
			.Map(statistics => (limit - statistics.CurrentAvailablePermits + statistics.CurrentQueuedCount) / (double)limit)
			.GetValueOr(0);
	}

	private static SlidingWindowRateLimiter Paced(int permits, double perSecond)
	{
		return new SlidingWindowRateLimiter(
			new SlidingWindowRateLimiterOptions
			{
				PermitLimit = permits,
				Window = TimeSpan.FromSeconds(permits / perSecond),
				SegmentsPerWindow = 4,
				QueueLimit = int.MaxValue,
			}
		);
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
