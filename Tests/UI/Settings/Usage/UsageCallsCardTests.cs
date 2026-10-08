using System.Globalization;
using System.Text.Json;
using BlazorBlueprint.Components;
using Bunit;
using Contracts.Usage;
using Domain.Providers;
using Domain.Usage;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Settings.Components.Usage;

namespace UI.Tests.Settings.Usage;

public class UsageCallsCardTests : BunitContext
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
	public async Task Render_TheCallsTab_ListsEveryOutcomeWithAnsweredInItsGrey()
	{
		var cut = RenderCard(UsageSamples.Snapshot().Current);

		var swatches = cut.FindAll("[data-testid=usage-outcome-legend] > span")
			.Select(item => $"{item.TextContent.Trim()}|{item.QuerySelector("span")?.GetAttribute("style")}");

		await Assert.That(swatches.First()).IsEqualTo("Answered|background: var(--usage-answered)");
		await Assert.That(swatches).Count().IsEqualTo(5);
	}

	[Test]
	public async Task Service_Picked_NarrowsTheCallsChartToItAndAClickAgainShowsAll()
	{
		var cut = RenderCard(UsageSamples.Snapshot().Current);

		await Row(cut, "Plex").ClickAsync();

		await Assert.That(Answered(cut)).IsEqualTo(40);
		await Assert.That(Row(cut, "Plex").GetAttribute("data-picked")).IsEqualTo("true");
		await Assert.That(cut.Find("[data-testid=usage-calls]").TextContent).Contains("Plex: last 24 hours, each hour split by how its calls ended");

		await Row(cut, "Plex").ClickAsync();

		await Assert.That(Answered(cut)).IsEqualTo(130);
		await Assert.That(cut.Find("[data-testid=usage-calls]").TextContent).Contains("All services: last 24 hours");
	}

	[Test]
	public async Task Service_PickedOnTheAnswerTimeTab_ShowsOnlyItsAnswers()
	{
		var cut = RenderCard(UsageSamples.Snapshot().Current);
		await cut.FindAll("button").First(button => button.TextContent.Trim() == "Answer time").ClickAsync();

		await Assert.That(cut.Find("[data-testid=usage-calls]").TextContent).Contains("All but datasets: every attempt by how long it took");

		await Row(cut, "TMDb").ClickAsync();

		await Assert.That(cut.Find("[data-testid=usage-calls]").TextContent).Contains("TMDb: every attempt by how long it took");
		await Assert.That(cut.Find("[data-testid=usage-latency-marks]").TextContent).Contains("median").And.Contains("p95");
	}

	[Test]
	public async Task Service_PickedButAbsentFromTheNextWindow_FallsBackToAll()
	{
		var cut = RenderCard(UsageSamples.Snapshot().Current);
		await Row(cut, "Plex").ClickAsync();

		cut.Render(parameters => parameters.Add(card => card.Period, UsageSamples.Snapshot().Previous));

		await Assert.That(Answered(cut)).IsEqualTo(50);
		await Assert.That(cut.Find("[data-testid=usage-calls]").TextContent).Contains("All services:");
	}

	[Test]
	public async Task RightNow_ForEachState_SaysWhatIsHappeningWithTheService()
	{
		var tally = UsageSamples.Tally(answered: 10);
		var period = UsageSamples.Period(
			[
				UsageSamples.Line(RemoteService.Plex, tally, UsageSamples.Quiet with { PausedUntil = Some.Of(At), LastCall = Some.Of(At) }),
				UsageSamples.Line(RemoteService.Sonarr, tally, UsageSamples.Quiet with { FailingSince = Some.Of(At), LastCall = Some.Of(At) }),
				UsageSamples.Line(RemoteService.Mdblist, tally, UsageSamples.Quiet with { RequestsLeft = Some.Of(962L), LastCall = Some.Of(At) }),
				UsageSamples.Line(RemoteService.ImdbDatasets, tally, UsageSamples.Quiet with { DownloadedAt = Some.Of(At), LastCall = Some.Of(At) }),
				UsageSamples.Line(RemoteService.Tmdb, tally, UsageSamples.Quiet with { LastCall = Some.Of(At) }),
			],
			[]
		);

		var cut = RenderCard(period);

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
		var cut = RenderCard(UsageSamples.Snapshot().Current);

		var silent = cut.Find("[data-testid=usage-silent]").TextContent;
		await Assert.That(silent).Contains("No calls in the last 24 hours").And.Contains("Wikidata").And.DoesNotContain("TMDb");
	}

	private static AngleSharp.Dom.IElement Row(IRenderedComponent<UsageCallsCard> cut, string name)
	{
		return cut.FindAll("[data-testid=usage-service]").Single(row => row.TextContent.Contains(name));
	}

	private static long Answered(IRenderedComponent<UsageCallsCard> cut)
	{
		var columns = JsonSerializer.SerializeToElement(cut.FindComponent<BbBarChart>().Instance.Data);
		return columns.EnumerateArray().Sum(column => column.GetProperty("Answered").GetInt64());
	}

	private IRenderedComponent<UsageCallsCard> RenderCard(UsagePeriod period)
	{
		return Render<UsageCallsCard>(parameters => parameters.Add(card => card.Period, period).Add(card => card.Range, UsageRange.Day));
	}
}
