using System.Net;
using Contracts.Providers;
using Domain.Providers;
using Microsoft.Extensions.Logging;
using Shared.Concurrency;

namespace API.Http.Lanes;

internal sealed class LaneHandler(RemoteService remote, Lanes lanes, IProviderRateLimitReporter reporter, TimeProvider time, ILogger<LaneHandler> logger)
	: DelegatingHandler
{
	internal static readonly HttpRequestOptionsKey<string> Owner = new("sammlarr.lane-owner");

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var name = lanes.Name(remote, request);
		var gate = lanes.Gate(name, request.TriggeredBy());
		var entered = await gate.EnterAsync(LanePriority.Current, lanes.Patience(remote), cancellationToken);
		var permit = entered.GetOrThrow(error => error.CausedBy<LaneRefusedException>().GetValueOr(() => new LaneRefusedException(error.Reason)));
		Optional.Of(request.GetResilienceContext()).Apply(context => context.Properties.Set(UsageStamp.Sent, true));
		var sending = base.SendAsync(request, cancellationToken);
		await ((Task)sending).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
		var outcome = Outcome(sending);
		permit.Release(outcome);
		await ReportAsync(name, outcome, sending, cancellationToken);
		return await sending;
	}

	private AttemptOutcome Outcome(Task<HttpResponseMessage> sending)
	{
		if (!sending.IsCompletedSuccessfully)
		{
			return sending.IsCanceled ? new AttemptAbandoned() : new AttemptFailed();
		}

		return sending.Result.StatusCode switch
		{
			HttpStatusCode.TooManyRequests => new AttemptThrottled(RetryAt(sending.Result)),
			HttpStatusCode.RequestTimeout or >= HttpStatusCode.InternalServerError => new AttemptFailed(),
			_ => new AttemptAnswered(),
		};
	}

	private Optional<DateTimeOffset> RetryAt(HttpResponseMessage response)
	{
		return Optional
			.Of(response.Headers.RetryAfter)
			.Map(after => Optional.Of(after.Date).Or(() => Optional.Of(after.Delta).Map(delta => time.GetUtcNow() + delta)));
	}

	private async Task ReportAsync(LaneName name, AttemptOutcome outcome, Task<HttpResponseMessage> sending, CancellationToken cancellationToken)
	{
		if (outcome is AttemptThrottled throttled)
		{
			var wait = throttled.RetryAt.Map(until => until - time.GetUtcNow()).GetValueOr(() => lanes.Backoff(name));
			logger.LogWarning("{Remote} answered 429 on its {Route} lane, which waits {Wait}; headers: {Headers}", remote, name.Route, wait, Headers(sending.Result));
			await sending.Result.TriggeredBy().Apply(user => reporter.ReportAsync(remote, wait, user, cancellationToken));
		}
	}

	private static string Headers(HttpResponseMessage response)
	{
		return string.Join(
			"; ",
			response
				.Headers.Concat(response.Content.Headers)
				.Where(header => !header.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
				.Select(header => $"{header.Key}: {string.Join(", ", header.Value)}")
		);
	}
}
