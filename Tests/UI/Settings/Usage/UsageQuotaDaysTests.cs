using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts.Usage;
using Domain.Usage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.Usage;

namespace UI.Tests.Settings.Usage;

public class UsageQuotaDaysTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_AWeek_DrawsABarPerQuotaDayAndCallsTodaysSoFar()
	{
		var cut = RenderDays(UsageSamples.Week(UsageSamples.Quota(UsageSamples.Allowance(191))), UsageRange.Week);

		await Assert.That(cut.FindAll("[data-testid=usage-quota-day]")).Count().IsEqualTo(7);
		await Assert
			.That(cut.FindAll("[data-testid=usage-quota-day-total]").Select(total => total.TextContent))
			.IsEquivalentTo(["654", "696", "627", "865", "691", "950", "809 so far"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Render_WithTheAllowance_DrawsItAndWhereBothFloorsStart()
	{
		var cut = RenderDays(UsageSamples.Week(UsageSamples.Quota(UsageSamples.Allowance(191))), UsageRange.Week);

		await Assert.That(cut.FindAll("[data-testid=usage-quota-days-allowance]")).Count().IsEqualTo(1);
		await Assert.That(cut.FindAll("[data-testid=usage-quota-days-floor]").Select(floor => floor.GetAttribute("data-used")!)).IsEquivalentTo(["500", "950"]);
		await Assert.That(cut.Markup).Contains("1,000 a day").And.Contains("Filling details stops after 500").And.Contains("Title lookups wait after 950");
	}

	[Test]
	public async Task Render_WithoutAnAnswer_ScalesToTheBusiestDayWithoutFloors()
	{
		var cut = RenderDays(UsageSamples.Week(UsageSamples.Unanswered()), UsageRange.Week);

		await Assert.That(cut.FindAll("[data-testid=usage-quota-days-floor]")).IsEmpty();
		await Assert.That(cut.FindAll("[data-testid=usage-quota-days-allowance]")).IsEmpty();
		await Assert.That(cut.FindAll("[data-testid=usage-quota-day]")).Count().IsEqualTo(7);
	}

	[Test]
	public async Task Render_ThirtyDays_TotalsOnlyToday()
	{
		var quota = UsageSamples.Unanswered() with
		{
			Days =
			[
				.. Enumerable
					.Range(0, 30)
					.Select(day => new QuotaDayUse(UsageSamples.Reset.AddDays(day - 29), new Dictionary<UsageCause, long> { [UsageCause.FeedRun] = 10 })),
			],
		};

		var cut = RenderDays(quota, UsageRange.Month);

		await Assert.That(cut.FindAll("[data-testid=usage-quota-day]")).Count().IsEqualTo(30);
		await Assert.That(cut.FindAll("[data-testid=usage-quota-day-total]").Select(total => total.TextContent)).IsEquivalentTo(["10 so far"]);
	}

	[Test]
	public async Task Day_PointedAt_ListsWhatSpentItsRequests()
	{
		var cut = Render<Harness>();

		await cut.FindAll("[data-testid=usage-quota-day]")[^1].TriggerEventAsync("onmouseenter", new MouseEventArgs());

		var tip = cut.WaitForElement("[data-portal-content][data-state=open] [data-testid=usage-quota-day-tip]");
		await Assert.That(tip.QuerySelector("div")!.TextContent.Trim()).IsEqualTo("Today · 809 requests");
		await Assert
			.That(
				tip.QuerySelectorAll("[data-testid=usage-quota-day-cause]")
					.Select(row => string.Join("|", row.Children.Skip(1).Select(cell => cell.TextContent.Trim())))
			)
			.IsEquivalentTo(
				["Feed runs|382", "Filling details|199", "Showing titles|139", "Feed editor|65", "Search|12", "Untagged|11", "Connection tests|1"],
				CollectionOrdering.Matching
			);
	}

	private IRenderedComponent<UsageQuotaDays> RenderDays(UsageQuota quota, UsageRange range)
	{
		return Render<UsageQuotaDays>(parameters => parameters.Add(days => days.Quota, quota).Add(days => days.Range, range));
	}

	private sealed class Harness : ComponentBase
	{
		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<UsageQuotaDays>(1);
			builder.AddComponentParameter(2, nameof(UsageQuotaDays.Quota), UsageSamples.Week(UsageSamples.Quota(UsageSamples.Allowance(191))));
			builder.AddComponentParameter(3, nameof(UsageQuotaDays.Range), UsageRange.Week);
			builder.CloseComponent();
		}
	}
}
