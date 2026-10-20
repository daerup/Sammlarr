using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using API.Http;
using API.Http.Lanes;
using Contracts.Providers;
using Contracts.Usage;
using Domain.Feed;
using Domain.Providers;
using Domain.Usage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Observability.Usage;
using Shared.DI;
using Shared.Options;

namespace API.Tests.Http;

public class UsageHandlersTests
{
	private const string PipelineDuration = "resilience.polly.pipeline.duration";

	private const string RequestDuration = "http.client.request.duration";

	[Test]
	public async Task SendAsync_RetriedAfterA429_IsOneCallAnsweredAfter429AndTwoAttempts()
	{
		var feed = FeedId.From(Guid.NewGuid());
		var answered = 0;
		await using var server = await ServerAsync(context =>
		{
			if (Interlocked.Increment(ref answered) == 1)
			{
				context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
				context.Response.Headers.RetryAfter = "1";
			}

			return Task.CompletedTask;
		});
		using var measured = new Measured();
		var feedRuns = IFeedRunUsage.Mock();
		using var host = Host(feedRuns: feedRuns.Object);

		await SendAsync(host, "plex", server, feed);

		var calls = measured.Of(PipelineDuration, feed);
		var attempts = measured.Of(RequestDuration, feed);
		await Assert
			.That(calls.Select(tags => (tags[UsageTags.Service], tags[UsageTags.Cause], tags[UsageTags.Outcome])))
			.IsEquivalentTo([("Plex", nameof(UsageCause.FeedRun), nameof(UsageOutcome.AnsweredAfter429))]);
		await Assert
			.That(attempts.Select(tags => (tags[UsageTags.Service], tags[UsageTags.Cause])))
			.IsEquivalentTo([("Plex", nameof(UsageCause.FeedRun)), ("Plex", nameof(UsageCause.FeedRun))]);
		feedRuns.Credit(feed, RemoteService.Plex, UsageOutcome.AnsweredAfter429, 1).WasCalled(Times.Once);
		feedRuns.Time(feed, RemoteService.Plex, Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task SendAsync_ForAFeedEditorCallOfAFeed_IsNotCreditedToThatFeedsRun()
	{
		var feed = FeedId.From(Guid.NewGuid());
		await using var server = await ServerAsync(_ => Task.CompletedTask);
		var feedRuns = IFeedRunUsage.Mock();
		using var host = Host(feedRuns: feedRuns.Object);

		using (UsageCauses.Mark(UsageCause.FeedEditor, Some.Of(feed)))
		{
			using var response = await host.Services.GetRequiredService<IHttpClientFactory>().CreateClient("plex").GetAsync(server.Urls.Single());
		}

		feedRuns.Credit(Any(), Any(), Any(), Any()).WasNeverCalled();
		feedRuns.Time(Any(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task SendAsync_CancelledByTheCaller_IsOneCancelledCall()
	{
		var feed = FeedId.From(Guid.NewGuid());
		await using var server = await ServerAsync(context => Task.Delay(Timeout.Infinite, context.RequestAborted));
		using var measured = new Measured();
		using var host = Host();
		using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

		await Assert.ThrowsAsync<OperationCanceledException>(() => SendAsync(host, "plex", server, feed, cancel.Token));

		await Assert.That(measured.Of(PipelineDuration, feed).Select(tags => tags[UsageTags.Outcome])).IsEquivalentTo([nameof(UsageOutcome.Cancelled)]);
	}

	[Test]
	public async Task SendAsync_UntilItsRouteGoesDown_IsAFailedCallWhileTheNextOneIsNeverSent()
	{
		var tried = FeedId.From(Guid.NewGuid());
		var refused = FeedId.From(Guid.NewGuid());
		await using var server = await ServerAsync(context =>
		{
			context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
			context.Response.Headers.RetryAfter = "1";
			return Task.CompletedTask;
		});
		using var measured = new Measured();
		using var host = Host(retryAttempts: 2, failuresToDown: 2);

		await SendAsync(host, "plex", server, tried);
		await Assert.ThrowsAsync<LaneRefusedException>(() => SendAsync(host, "plex", server, refused));

		await Assert.That(measured.Of(PipelineDuration, tried).Select(tags => tags[UsageTags.Outcome])).IsEquivalentTo([nameof(UsageOutcome.Failed)]);
		await Assert.That(measured.Of(PipelineDuration, refused)).IsEmpty();
	}

	[Test]
	public async Task SendAsync_On429BeyondItsMaxWait_IsOneCallThatGaveUpOn429()
	{
		var feed = FeedId.From(Guid.NewGuid());
		await using var server = await ServerAsync(context =>
		{
			context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
			context.Response.Headers.RetryAfter = "60";
			return Task.CompletedTask;
		});
		using var measured = new Measured();
		using var host = Host();

		await SendAsync(host, "plex", server, feed);

		await Assert.That(measured.Of(PipelineDuration, feed).Select(tags => tags[UsageTags.Outcome])).IsEquivalentTo([nameof(UsageOutcome.GaveUpOn429)]);
	}

	[Test]
	public async Task SendAsync_CancelledWhileQueuedInItsLane_IsNoCall()
	{
		var held = FeedId.From(Guid.NewGuid());
		var queued = FeedId.From(Guid.NewGuid());
		var release = new TaskCompletionSource();
		await using var server = await ServerAsync(_ => release.Task);
		using var measured = new Measured();
		using var host = Host(maxInFlight: 1);
		var holding = SendAsync(host, "plex", server, held);
		using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

		await Assert.ThrowsAsync<OperationCanceledException>(() => SendAsync(host, "plex", server, queued, cancel.Token));
		release.SetResult();
		await holding;

		await Assert.That(measured.Of(PipelineDuration, queued)).IsEmpty();
		await Assert.That(measured.Of(PipelineDuration, held).Select(tags => tags[UsageTags.Outcome])).IsEquivalentTo([nameof(UsageOutcome.Answered)]);
	}

	[Test]
	public async Task SendAsync_ThroughAClientHttpClient_IsNotTagged()
	{
		var feed = FeedId.From(Guid.NewGuid());
		await using var server = await ServerAsync(_ => Task.CompletedTask);
		using var measured = new Measured();
		using var host = Host();

		await SendAsync(host, "pictures", server, feed);

		await Assert.That(measured.Of(PipelineDuration, feed)).IsEmpty();
		await Assert.That(measured.Of(RequestDuration, feed)).IsEmpty();
		await Assert.That(measured.To(server).Select(tags => tags.ContainsKey(UsageTags.Service))).IsEquivalentTo([false]);
	}

	private static async Task SendAsync(IHost host, string client, WebApplication server, FeedId feed, CancellationToken ct = default)
	{
		using var marked = UsageCauses.Mark(feed);
		using var response = await host.Services.GetRequiredService<IHttpClientFactory>().CreateClient(client).GetAsync(server.Urls.Single(), ct);
	}

	private static async Task<WebApplication> ServerAsync(RequestDelegate answer)
	{
		var builder = WebApplication.CreateSlimBuilder();
		builder.WebHost.UseUrls("http://127.0.0.1:0");
		var server = builder.Build();
		server.Run(answer);
		await server.StartAsync();
		return server;
	}

	private static IHost Host(int retryAttempts = 1, int failuresToDown = 4, int maxInFlight = int.MaxValue, IFeedRunUsage? feedRuns = null)
	{
		return new HostBuilder()
			.ConfigureServices(services =>
			{
				services.AddSingleton(
					new PlexOptions
					{
						BaseUrl = "https://plex.test",
						Authentication = new OAuthenticationOptions { ProductName = "Sammlarr" },
						Http = new ProviderHttpOptions
						{
							RetryAttempts = retryAttempts,
							MaxWaitSeconds = 5,
							Lanes = new Dictionary<string, LaneOptions>
							{
								["api"] = new LaneOptions { FailuresToDown = failuresToDown, MaxInFlight = maxInFlight },
							},
						},
					}
				);
				services.AddSingleton(IProviderRateLimitReporter.Mock().Object);
				services.AddSingleton(feedRuns ?? IFeedRunUsage.Mock().Object);
				services.AddModule<HttpModule>();
				services.AddHttpClient("plex").AddRemoteHttp<PlexOptions>(RemoteService.Plex);
				services.AddHttpClient("pictures").AddClientHttp<PlexOptions>();
			})
			.Build();
	}

	private sealed class Measured : IDisposable
	{
		private readonly MeterListener _listener = new();

		private readonly ConcurrentQueue<(string Instrument, Dictionary<string, string> Tags)> _seen = new();

		public Measured()
		{
			_listener.InstrumentPublished = (instrument, listener) =>
			{
				if (instrument.Name is PipelineDuration or RequestDuration)
				{
					listener.EnableMeasurementEvents(instrument);
				}
			};
			_listener.SetMeasurementEventCallback<double>(
				(instrument, _, tags, _) => _seen.Enqueue((instrument.Name, tags.ToArray().ToDictionary(tag => tag.Key, tag => $"{tag.Value}")))
			);
			_listener.Start();
		}

		public IReadOnlyList<Dictionary<string, string>> Of(string instrument, FeedId feed)
		{
			return
			[
				.. _seen.Where(seen => seen.Instrument == instrument && seen.Tags.GetValueOrDefault(UsageTags.Feed) == feed.ToString()).Select(seen => seen.Tags),
			];
		}

		public IReadOnlyList<Dictionary<string, string>> To(WebApplication server)
		{
			var port = new Uri(server.Urls.Single()).Port.ToString();
			return [.. _seen.Where(seen => seen.Instrument == RequestDuration && seen.Tags.GetValueOrDefault("server.port") == port).Select(seen => seen.Tags)];
		}

		public void Dispose() => _listener.Dispose();
	}
}
