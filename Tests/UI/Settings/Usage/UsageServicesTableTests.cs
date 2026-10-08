using System.Globalization;
using BlazorBlueprint.Components;
using Bunit;
using Contracts.Usage;
using Domain.Providers;
using Domain.Usage;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Settings.Components.Usage;

namespace UI.Tests.Settings.Usage;

public class UsageServicesTableTests : BunitContext
{
	private static readonly DateTimeOffset At = new(2026, 10, 7, 9, 30, 0, TimeSpan.Zero);

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		Services.AddSingleton(UsageSamples.Clock());
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task RightNow_ForEachState_SaysWhatIsHappeningWithTheService()
	{
		var tally = UsageSamples.Tally(answered: 10);
		var period = UsageSamples.Period(
			[
				new UsageServiceLine(RemoteService.Plex, tally, UsageSamples.Quiet with { PausedUntil = Some.Of(At), LastCall = Some.Of(At) }),
				new UsageServiceLine(RemoteService.Sonarr, tally, UsageSamples.Quiet with { FailingSince = Some.Of(At), LastCall = Some.Of(At) }),
				new UsageServiceLine(RemoteService.Mdblist, tally, UsageSamples.Quiet with { RequestsLeft = Some.Of(962L), LastCall = Some.Of(At) }),
				new UsageServiceLine(RemoteService.ImdbDatasets, tally, UsageSamples.Quiet with { DownloadedAt = Some.Of(At), LastCall = Some.Of(At) }),
				new UsageServiceLine(RemoteService.Tmdb, tally, UsageSamples.Quiet with { LastCall = Some.Of(At) }),
			],
			[]
		);

		var cut = RenderTable(period);

		var clock = At.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
		var rightNow = cut.FindAll("[data-testid=usage-service]")
			.ToDictionary(row => row.GetAttribute("data-service")!, row => row.QuerySelector("[data-testid=usage-right-now]")!.TextContent.Trim());
		await Assert.That(rightNow["Plex"]).IsEqualTo($"Paused until {clock}");
		await Assert.That(rightNow["Sonarr"]).IsEqualTo($"Not answering since {clock}");
		await Assert.That(rightNow["Mdblist"]).IsEqualTo("962 requests left today");
		await Assert.That(rightNow["ImdbDatasets"]).IsEqualTo($"Downloaded at {clock}");
		await Assert.That(rightNow["Tmdb"]).IsEqualTo($"Last call {clock}");
	}

	[Test]
	public async Task Render_ServicesWithoutCalls_AreNamedUnderTheTable()
	{
		var cut = RenderTable(UsageSamples.Snapshot().Current);

		var silent = cut.Find("[data-testid=usage-silent]").TextContent;
		await Assert.That(silent).Contains("No calls in the last 24 hours").And.Contains("Wikidata").And.DoesNotContain("TMDb");
	}

	private IRenderedComponent<UsageServicesTable> RenderTable(UsagePeriod period)
	{
		return Render<UsageServicesTable>(parameters => parameters.Add(table => table.Period, period).Add(table => table.Range, UsageRange.Day));
	}
}
