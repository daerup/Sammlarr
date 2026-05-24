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
			Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
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
	public async Task IdResolution_WithCandidates_TagsCandidatesAndOmitsThemFromDisplayName()
	{
		using var activity = SammlarrTelemetry.StartIdResolution(MediaProvider.Tmdb, [PlexId.From("abc", MediaType.Movie)]);

		await Assert.That(activity!.DisplayName).DoesNotContain("Plex:Movie:abc");
		await Assert.That(activity.GetTagItem("id_resolution.candidates")).IsEquivalentTo(new[] { "Plex:Movie:abc" });
	}

	[Test]
	public async Task TagIdResolutionOutcome_AppendsUppercaseSuffixAndTagsOutcome()
	{
		using var activity = SammlarrTelemetry.StartIdResolution(MediaProvider.Tmdb, [PlexId.From("abc", MediaType.Movie)]);
		SammlarrTelemetry.TagIdResolutionOutcome(activity, IdResolutionOutcome.Hit, MediaProvider.Tmdb);

		await Assert.That(activity!.DisplayName).EndsWith(" [HIT]");
		await Assert.That(activity.GetTagItem("id_resolution.outcome")).IsEqualTo("hit");
	}

	[Test]
	public async Task TagItemOutcome_AppendsUppercaseSuffixAndTagsOutcome()
	{
		using var activity = SammlarrTelemetry.StartMetadataItem(MediaId.From(Guid.NewGuid()));
		SammlarrTelemetry.TagItemOutcome(activity, MetadataItemOutcome.Stale, MediaType.Movie);

		await Assert.That(activity!.DisplayName).EndsWith(" [STALE]");
		await Assert.That(activity.GetTagItem("metadata.outcome")).IsEqualTo("stale");
	}

	[Test]
	public async Task IdResolution_WithTitle_TagsTitleAndOmitsItFromDisplayName()
	{
		using var activity = SammlarrTelemetry.StartIdResolution(MediaProvider.Plex, [TmdbId.From("1", MediaType.Movie)], "Inception");

		await Assert.That(activity!.DisplayName).DoesNotContain("Inception");
		await Assert.That(activity.GetTagItem("id_resolution.title")).IsEqualTo("Inception");
	}
}
