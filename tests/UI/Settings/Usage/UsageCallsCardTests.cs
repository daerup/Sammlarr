using System.Globalization;
using System.Text.Json;
using BlazorBlueprint.Components;
using Bunit;
using Contracts.Usage;
using Domain.Providers;
using Domain.Usage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
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
	[Arguments("Enter")]
	[Arguments(" ")]
	public async Task Row_WhenEnterOrSpaceIsPressed_PicksItsService(string key)
	{
		Optional<RemoteService> raised = new None();
		var cut = RenderCard(UsageSamples.Snapshot().Current, changed: picked => raised = picked);

		await Row(cut, "Plex").KeyDownAsync(new KeyboardEventArgs { Key = key });

		await Assert.That(raised).IsSome().And.IsEqualTo(RemoteService.Plex);
	}

	[Test]
	public async Task Row_Clicked_PicksItsService()
	{
		Optional<RemoteService> raised = new None();
		var cut = RenderCard(UsageSamples.Snapshot().Current, changed: picked => raised = picked);

		await Row(cut, "Plex").ClickAsync();

		await Assert.That(raised).IsSome().And.IsEqualTo(RemoteService.Plex);
	}

	[Test]
	public async Task Row_ClickedWhileItsServiceIsPicked_ShowsEveryServiceAgain()
	{
		Optional<RemoteService> raised = Some.Of(RemoteService.Plex);
		var cut = RenderCard(UsageSamples.Snapshot().Current, Some.Of(RemoteService.Plex), picked => raised = picked);

		await Row(cut, "Plex").ClickAsync();

		await Assert.That(raised).IsNone();
	}

	[Test]
	public async Task Picked_AService_ShowsItsChipAndNarrowsTheChartToItsCalls()
	{
		var cut = RenderCard(UsageSamples.Snapshot().Current, Some.Of(RemoteService.Plex));

		await Assert.That(Answered(cut)).IsEqualTo(40);
		await Assert.That(Row(cut, "Plex").GetAttribute("aria-pressed")).IsEqualTo("true");
		await Assert.That(cut.Find("[data-testid=usage-pick-chip]").GetAttribute("data-service")).IsEqualTo("Plex");
		await Assert.That(cut.FindComponent<BbCardDescription>().Find("p").TextContent.Trim()).StartsWith("Plex: last 24 hours");
	}

	[Test]
	public async Task Picked_Nothing_ShowsEveryServiceAndNamesTheSilentOnes()
	{
		var cut = RenderCard(UsageSamples.Snapshot().Current);

		await Assert.That(Answered(cut)).IsEqualTo(130);
		await Assert.That(cut.FindAll("[data-testid=usage-pick-chip]")).IsEmpty();
		await Assert.That(cut.Find("[data-testid=usage-calls]").TextContent).Contains("All services: last 24 hours");
		var silent = cut.Find("[data-testid=usage-silent]").TextContent;
		await Assert.That(silent).Contains("No calls in the last 24 hours").And.Contains("Wikidata").And.DoesNotContain("TMDb");
	}

	[Test]
	public async Task Chip_Cleared_ShowsEveryServiceAgain()
	{
		Optional<RemoteService> raised = Some.Of(RemoteService.Plex);
		var cut = RenderCard(UsageSamples.Snapshot().Current, Some.Of(RemoteService.Plex), picked => raised = picked);

		await cut.Find("[data-testid=usage-pick-chip] button[aria-label='Show every service']").ClickAsync();

		await Assert.That(raised).IsNone();
	}

	[Test]
	public async Task Service_PickedOnTheAnswerTimeTab_ShowsOnlyItsAnswers()
	{
		var cut = RenderCard(UsageSamples.Snapshot().Current);
		await cut.FindAll("button").First(button => button.TextContent.Trim() == "Answer time").ClickAsync();

		await Assert.That(cut.Find("[data-testid=usage-calls]").TextContent).Contains("All but datasets: every attempt by how long it took");

		cut.Render(parameters => parameters.Add(card => card.Picked, Some.Of(RemoteService.Tmdb)));

		await Assert.That(cut.Find("[data-testid=usage-calls]").TextContent).Contains("TMDb: every attempt by how long it took");
		await Assert.That(Marks(cut).Select(mark => mark.TextContent.Trim())).IsEquivalentTo(["median 88 ms", "p95 262 ms"]);
	}

	[Test]
	public async Task Tab_AnswerTime_DrawsTheMedianAndP95AsLinesWhereTheyFallInTheirSquares()
	{
		var cut = RenderCard(UsageSamples.Snapshot().Current);

		await cut.FindAll("button").First(button => button.TextContent.Trim() == "Answer time").ClickAsync();

		var marks = Marks(cut);
		await Assert.That(marks.Select(mark => mark.TextContent.Trim())).IsEquivalentTo(["median 88 ms", "p95 100 ms"]);
		await Assert.That(marks[0].GetAttribute("style")).Contains("left: calc(48px + (100% - 52px) * 0.3679)");
	}

	[Test]
	public async Task Picked_AServiceWithoutCallsInTheWindow_KeepsItsChipAndSaysSo()
	{
		var cut = Render<UsageCallsCard>(parameters =>
			parameters
				.Add(card => card.Period, UsageSamples.Snapshot().Current)
				.Add(card => card.Range, UsageRange.Week)
				.Add(card => card.Picked, Some.Of(RemoteService.Mdblist))
		);

		await Assert.That(cut.Find("[data-testid=usage-calls-none]").TextContent).IsEqualTo("No MDBList calls in the last 7 days.");
		await Assert.That(cut.Find("[data-testid=usage-pick-chip]").GetAttribute("data-service")).IsEqualTo("Mdblist");
		await Assert.That(cut.FindAll("[data-testid=usage-service][aria-pressed=true]")).IsEmpty();
	}

	[Test]
	public async Task Picked_AServiceWithADailyQuota_OpensItsSummaryAndLineUnderItsRow()
	{
		var period = UsageSamples.Period(
			[UsageSamples.Line(RemoteService.Tmdb, UsageSamples.Tally(answered: 90)), UsageSamples.Line(RemoteService.Mdblist, UsageSamples.Tally(answered: 40))],
			[]
		);

		var cut = RenderCard(period, Some.Of(RemoteService.Mdblist), quotas: [UsageSamples.Quota(UsageSamples.Allowance(191))]);

		var panel = cut.Find("[data-testid=usage-quota-panel]");
		await Assert.That(panel.PreviousElementSibling!.GetAttribute("data-service")).IsEqualTo("Mdblist");
		await Assert.That(panel.QuerySelector("[data-testid=usage-quota-summary]")!.TextContent).Contains("191").And.Contains("of 1,000 left today");
		await Assert.That(panel.QuerySelector("[data-testid=usage-quota-line]")).IsNotNull();
	}

	[Test]
	public async Task Picked_AServiceWithADailyQuotaOnSevenDays_OpensItsQuotaDays()
	{
		var cut = Render<UsageCallsCard>(parameters =>
			parameters
				.Add(card => card.Period, UsageSamples.Snapshot().Current)
				.Add(card => card.Range, UsageRange.Week)
				.Add(card => card.Quotas, [UsageSamples.Week(UsageSamples.Quota(UsageSamples.Allowance(191)))])
				.Add(card => card.Picked, Some.Of(RemoteService.Mdblist))
		);

		var panel = cut.Find("[data-testid=usage-quota-panel]");
		await Assert.That(panel.QuerySelector("[data-testid=usage-quota-days]")).IsNotNull();
		await Assert.That(panel.QuerySelector("[data-testid=usage-quota-line]")).IsNull();
		await Assert.That(panel.QuerySelector("[data-testid=usage-quota-summary]")!.TextContent).Contains("a day, resets at");
	}

	[Test]
	public async Task Picked_AServiceWithoutADailyQuota_OpensNothing()
	{
		var cut = RenderCard(UsageSamples.Snapshot().Current, Some.Of(RemoteService.Tmdb), quotas: [UsageSamples.Quota(UsageSamples.Allowance(191))]);

		await Assert.That(cut.FindAll("[data-testid=usage-quota-panel]")).IsEmpty();
	}

	[Test]
	public async Task Picked_AServiceWithADailyQuotaButNoCallsInTheWindow_StillOpensItsSummary()
	{
		var cut = RenderCard(UsageSamples.Snapshot().Current, Some.Of(RemoteService.Mdblist), quotas: [UsageSamples.Unanswered()]);

		await Assert.That(cut.Find("[data-testid=usage-calls-none]").NextElementSibling!.GetAttribute("data-testid")).IsEqualTo("usage-quota-panel");
		await Assert.That(cut.Find("[data-testid=usage-quota-summary]").TextContent).Contains("no answer since");
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

	private static AngleSharp.Dom.IElement Row(IRenderedComponent<UsageCallsCard> cut, string name)
	{
		return cut.FindAll("[data-testid=usage-service]").Single(row => row.TextContent.Contains(name));
	}

	private static IReadOnlyList<AngleSharp.Dom.IElement> Marks(IRenderedComponent<UsageCallsCard> cut) => cut.FindAll("[data-testid=usage-latency-mark]");

	private static long Answered(IRenderedComponent<UsageCallsCard> cut)
	{
		var columns = JsonSerializer.SerializeToElement(cut.FindComponent<BbBarChart>().Instance.Data);
		return columns.EnumerateArray().Sum(column => column.GetProperty("Answered").GetInt64());
	}

	private IRenderedComponent<UsageCallsCard> RenderCard(
		UsagePeriod period,
		Optional<RemoteService>? picked = null,
		Action<Optional<RemoteService>>? changed = null,
		IReadOnlyList<UsageQuota>? quotas = null
	)
	{
		return Render<UsageCallsCard>(parameters =>
			parameters
				.Add(card => card.Period, period)
				.Add(card => card.Range, UsageRange.Day)
				.Add(card => card.Quotas, quotas ?? [])
				.Add(card => card.Picked, picked ?? new None())
				.Add(card => card.PickedChanged, changed ?? (_ => { }))
		);
	}
}
