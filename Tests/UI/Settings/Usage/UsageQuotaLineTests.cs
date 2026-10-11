using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts.Usage;
using Domain.Usage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Settings.Components.Usage;

namespace UI.Tests.Settings.Usage;

public class UsageQuotaLineTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(UsageSamples.At(UsageSamples.Morning));
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_WithTheAllowance_DrawsTheHalfFloorAndTheReserve()
	{
		var cut = RenderLine(UsageSamples.Quota(UsageSamples.Allowance(191)));

		await Assert.That(cut.FindAll("[data-testid=usage-quota-floor]").Select(floor => floor.GetAttribute("data-left"))).IsEquivalentTo(["500", "50"]);
		await Assert.That(cut.Markup).Contains("Filling details stops at 500 left").And.Contains("Title lookups wait at 50 left, feed list pages may use the rest");
	}

	[Test]
	public async Task Render_WithoutTheAllowance_DrawsOnlyTheReserve()
	{
		var cut = RenderLine(UsageSamples.Quota(UsageSamples.Allowance(191) with { Allowance = new None() }));

		await Assert.That(cut.FindAll("[data-testid=usage-quota-floor]").Select(floor => floor.GetAttribute("data-left"))).IsEquivalentTo(["50"]);
		await Assert.That(cut.Markup).DoesNotContain("Filling details stops");
	}

	[Test]
	public async Task Render_LastNightsWait_ShadesItAndSaysWhenItRan()
	{
		var cut = RenderLine(UsageSamples.Quota(UsageSamples.Allowance(191)));

		await Assert.That(cut.FindAll("[data-testid=usage-quota-wait]")).Count().IsEqualTo(1);
		await Assert
			.That(cut.Find("[data-testid=usage-quota-waited]").TextContent)
			.IsEqualTo($"Title lookups waited {UsageSamples.Local(UsageSamples.Reset.AddHours(-4).AddMinutes(-56))} - {UsageSamples.Local(UsageSamples.Reset)}");
	}

	[Test]
	public async Task Render_OnceTheReserveIsReachedAgain_ShadesABandPerWait()
	{
		var allowance = UsageSamples.Allowance(50) with { ReserveReachedAt = Some.Of(UsageSamples.Morning.AddMinutes(-9)) };

		var cut = RenderLine(UsageSamples.Quota(allowance, [2, 419, 79, 1, 0, 4, 17, 65, 104, 259]));

		await Assert.That(cut.FindAll("[data-testid=usage-quota-wait]")).Count().IsEqualTo(2);
		await Assert.That(cut.FindAll("[data-testid=usage-quota-waited]")).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Render_Now_LabelsWhatIsLeft()
	{
		var cut = RenderLine(UsageSamples.Quota(UsageSamples.Allowance(191)));

		await Assert.That(cut.Find("[data-testid=usage-quota-now]").TextContent).IsEqualTo("191 left");
	}

	[Test]
	public async Task Render_WithoutTheDayBefore_StartsAtTheResetAndSaysWhy()
	{
		var cut = RenderLine(UsageSamples.Quota(UsageSamples.Allowance(191) with { DayBefore = new None() }));

		await Assert.That(cut.Find("[data-testid=usage-quota-path]").GetAttribute("d")).StartsWith("M583.33,0 ");
		await Assert
			.That(cut.Find("[data-testid=usage-quota-unknown]").TextContent)
			.IsEqualTo("Sammlarr did not see the day before end: before the reset it shows after tonight");
	}

	[Test]
	public async Task Render_WithTheDayBefore_DrawsItsHoursWithoutANote()
	{
		var cut = RenderLine(UsageSamples.Quota(UsageSamples.Allowance(191)));

		await Assert.That(cut.Find("[data-testid=usage-quota-path]").GetAttribute("d")).StartsWith("M0,68.2 ");
		await Assert.That(cut.FindAll("[data-testid=usage-quota-unknown]")).IsEmpty();
	}

	[Test]
	public async Task Render_WithRequestsUsedOutsideSammlarr_HatchesThemAtTheReset()
	{
		var cut = RenderLine(UsageSamples.Quota(UsageSamples.Allowance(71) with { Outside = Some.Of(120L) }));

		await Assert.That(cut.Find("[data-testid=usage-quota-outside]").GetAttribute("height")).IsEqualTo("13.2");
		await Assert.That(cut.Markup).Contains("120 used outside Sammlarr, time not known");
	}

	[Test]
	public async Task Render_WithoutAnAnswer_DrawsNothing()
	{
		var cut = RenderLine(UsageSamples.Unanswered());

		await Assert.That(cut.FindAll("[data-testid=usage-quota-line]")).IsEmpty();
	}

	[Test]
	public async Task Hour_PointedAt_ShowsItsDropAndTheCallsBehindIt()
	{
		var cut = Render<Harness>();

		await cut.FindAll("[data-testid=usage-quota-hour]")[15].TriggerEventAsync("onmouseenter", new MouseEventArgs());

		var tip = cut.WaitForElement("[data-portal-content][data-state=open] [data-testid=usage-quota-tip]");
		await Assert
			.That(tip.QuerySelector("div")!.TextContent.Trim())
			.IsEqualTo($"{UsageSamples.Local(UsageSamples.Reset.AddHours(1))} - {UsageSamples.Local(UsageSamples.Reset.AddHours(2))} · 998 → 579 left");
		await Assert
			.That(tip.QuerySelectorAll("[data-testid=usage-quota-tip-cause]").Select(row => string.Join("|", row.Children.Select(cell => cell.TextContent.Trim()))))
			.IsEquivalentTo(["|Feed runs|299", "|Filling details|120"]);
	}

	[Test]
	public async Task Hour_BeforeTheLineStarts_HasNothingToPointAt()
	{
		var cut = RenderLine(UsageSamples.Quota(UsageSamples.Allowance(191) with { DayBefore = new None() }));

		await Assert.That(cut.FindAll("[data-testid=usage-quota-hour]")).Count().IsEqualTo(10);
	}

	private IRenderedComponent<UsageQuotaLine> RenderLine(UsageQuota quota)
	{
		return Render<UsageQuotaLine>(parameters => parameters.Add(line => line.Quota, quota).Add(line => line.Window, UsageSamples.Hours));
	}

	private sealed class Harness : ComponentBase
	{
		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			IReadOnlyList<UsageSlot> slots =
			[
				.. Enumerable
					.Range(0, UsageSamples.Hours.Slots)
					.Select(slot => new UsageSlot(
						UsageSamples.Hours.SlotAt(slot),
						UsageTally.Empty,
						slot == 15 ? new Dictionary<UsageCause, long> { [UsageCause.DetailsFill] = 120, [UsageCause.FeedRun] = 299 } : new Dictionary<UsageCause, long>()
					)),
			];
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<UsageQuotaLine>(1);
			builder.AddComponentParameter(2, nameof(UsageQuotaLine.Quota), UsageSamples.Quota(UsageSamples.Allowance(191)));
			builder.AddComponentParameter(3, nameof(UsageQuotaLine.Window), UsageSamples.Hours);
			builder.AddComponentParameter(4, nameof(UsageQuotaLine.Slots), slots);
			builder.CloseComponent();
		}
	}
}
