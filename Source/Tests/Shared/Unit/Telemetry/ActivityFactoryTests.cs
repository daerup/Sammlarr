using System.Diagnostics;
using Contracts.Media;
using Shared.Telemetry;

namespace Shared.Tests.Unit.Telemetry;

public class ActivityFactoryTests
{
	private ActivityListener _listener = null!;

	[Before(HookType.Test)]
	public void StartListener()
	{
		_listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == SammlarrTelemetry.ServiceName,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
		};
		ActivitySource.AddActivityListener(_listener);
	}

	[After(HookType.Test)]
	public void StopListener() => _listener.Dispose();

	[Test]
	[Arguments("OnRenderCompleted")]
	[Arguments("EndInvokeJSFromDotNet")]
	public async Task BlazorHubInvocation_ForExcludedMethod_ReturnsNull(string excludedMethod)
	{
		using var activity = SammlarrTelemetry.StartBlazorHubInvocation(excludedMethod, "circuit");

		await Assert.That(activity).IsNull();
	}

	[Test]
	public async Task SetBatchCacheBreakdown_RewritesBatchDisplayName()
	{
		using var activity = SammlarrTelemetry.StartMetadataBatch(20);
		var initial = activity!.DisplayName;

		SammlarrTelemetry.SetBatchCacheBreakdown(activity, hits: 12, misses: 5, stale: 3);

		await Assert.That(activity.DisplayName).IsNotEqualTo(initial);
	}

	[Test]
	public async Task IdResolution_WithCandidates_IncludesCandidatesInDisplayName()
	{
		using var activity = SammlarrTelemetry.StartIdResolution(MediaProvider.Tmdb, (IReadOnlyCollection<IMediaId>)[PlexId.From("abc", MediaType.Movie)]);

		await Assert.That(activity!.DisplayName).Contains("Plex:Movie:abc");
	}

	[Test]
	public async Task IdResolution_WithTitle_IncludesTitleInDisplayName()
	{
		using var activity = SammlarrTelemetry.StartIdResolution(
			MediaProvider.Plex,
			(IReadOnlyCollection<IMediaId>)[TmdbId.From("1", MediaType.Movie)],
			"Inception"
		);

		await Assert.That(activity!.DisplayName).Contains("Inception");
	}
}
