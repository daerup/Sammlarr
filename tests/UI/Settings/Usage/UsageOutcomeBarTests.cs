using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.Usage;
using UI.Shared.Bars;

namespace UI.Tests.Settings.Usage;

public class UsageOutcomeBarTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Bar_PointedAt_ListsEveryOutcomeItHadWithCountAndShare()
	{
		var cut = Render<Harness>();

		await cut.Find("[data-testid=stacked-bar-track] [data-part]").TriggerEventAsync("onmouseenter", new MouseEventArgs());

		var tip = cut.WaitForElement("[data-portal-content][data-state=open] [data-testid=stacked-bar-tip]");
		await Assert.That(tip.TextContent).Contains("TMDb").And.Contains("· 2,616 calls");
		await Assert
			.That(tip.QuerySelectorAll("[data-testid=stacked-bar-tip-row]").Select(row => string.Join("|", row.Children.Select(cell => cell.TextContent.Trim()))))
			.IsEquivalentTo(["Answered|2,573|98.4%", "Rate-limited, then answered|43|1.6%"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Bar_AgainstTheBusiestService_FillsItsShareOfTheRow()
	{
		var cut = Render<Harness>();

		await Assert.That(cut.FindComponent<StackedBar>().Instance.Total).IsEqualTo(5232L);
	}

	private sealed class Harness : ComponentBase
	{
		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<UsageOutcomeBar>(1);
			builder.AddComponentParameter(2, nameof(UsageOutcomeBar.Tally), UsageSamples.Tally(answered: 2573, after: 43));
			builder.AddComponentParameter(3, nameof(UsageOutcomeBar.Busiest), 5232L);
			builder.AddComponentParameter(4, nameof(UsageOutcomeBar.Title), (RenderFragment)(title => title.AddContent(0, "TMDb")));
			builder.CloseComponent();
		}
	}
}
