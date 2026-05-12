using System.Collections.Concurrent;
using System.Diagnostics;
using Contracts.Auth;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Shared.Options;
using Shared.Telemetry;

namespace Shared.Tests.Unit.Telemetry;

public class TelemetryPipelineTests
{
	[Test]
	public async Task TelemetryModule_RegistersTracerProviderThatSampledActivities()
	{
		var captured = new ConcurrentBag<Activity>();
		await using var sp = BuildServices(captured);
		_ = sp.GetRequiredService<TracerProvider>();

		using (SammlarrTelemetry.StartBlazorHubInvocation("UpdateRootComponents", "circuit"))
		using (SammlarrTelemetry.StartBlacklistLoad(WatchlistProvider.Plex)) { }

		await Assert.That(captured.Select(a => a.OperationName)).Contains("blazor.UpdateRootComponents");
		await Assert.That(captured.Select(a => a.OperationName)).Contains("blacklist.load");
	}

	[Test]
	public async Task BlazorHubInvocation_IsServerKind_AndParentsInternalChildren()
	{
		var captured = new ConcurrentBag<Activity>();
		await using var sp = BuildServices(captured);
		_ = sp.GetRequiredService<TracerProvider>();

		using var blazor = SammlarrTelemetry.StartBlazorHubInvocation("UpdateRootComponents", "circuit");
		using var blacklist = SammlarrTelemetry.StartBlacklistLoad(WatchlistProvider.Plex);

		await Assert.That(blazor!.Kind).IsEqualTo(ActivityKind.Server);
		await Assert.That(blacklist!.Kind).IsEqualTo(ActivityKind.Internal);
		await Assert.That(blacklist.ParentSpanId).IsEqualTo(blazor.SpanId);
	}

	private static ServiceProvider BuildServices(ConcurrentBag<Activity> captured)
	{
		var services = new ServiceCollection();
		services.AddSingleton(new TelemetryOptions { Verbosity = TelemetryVerbosity.Verbose });
		new TelemetryModule().RegisterServices(services);
		services.AddOpenTelemetry().WithTracing(t => t.AddProcessor(new CapturingProcessor(captured)));
		return services.BuildServiceProvider();
	}

	private sealed class CapturingProcessor(ConcurrentBag<Activity> captured) : BaseProcessor<Activity>
	{
		public override void OnEnd(Activity data) => captured.Add(data);
	}
}
