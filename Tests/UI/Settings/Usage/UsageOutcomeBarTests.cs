using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using UI.Features.Settings.Components.Usage;

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

		await cut.Find("[data-testid=usage-outcome-bar]").ParentElement!.TriggerEventAsync("onmouseenter", new MouseEventArgs());

		var tip = cut.WaitForElement("[data-portal-content][data-state=open] [data-testid=usage-outcome-tip]");
		await Assert.That(tip.TextContent).Contains("TMDb").And.Contains("· 2,616 calls");
		await Assert
			.That(
				tip.QuerySelectorAll("[data-testid=usage-outcome-tip-row]")
					.Select(row => string.Join("|", row.Children.Skip(2).Select(cell => cell.TextContent.Trim())))
			)
			.IsEquivalentTo(["Answered|2,573|98.4%", "Rate-limited, then answered|43|1.6%"]);
	}

	[Test]
	public async Task Segment_PointedAt_SetsItsOutcomeInBold()
	{
		var cut = Render<Harness>();

		await cut.Find("[data-outcome=AnsweredAfter429]").TriggerEventAsync("onmouseenter", new MouseEventArgs());
		await cut.Find("[data-testid=usage-outcome-bar]").ParentElement!.TriggerEventAsync("onmouseenter", new MouseEventArgs());

		var rows = cut.WaitForElement("[data-portal-content][data-state=open] [data-testid=usage-outcome-tip]").QuerySelectorAll("[data-at=true]");
		await Assert.That(rows.Select(row => row.TextContent)).Count().IsEqualTo(1);
		await Assert.That(rows[0].TextContent).Contains("Rate-limited, then answered");
	}

	private sealed class Harness : ComponentBase
	{
		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<UsageOutcomeBar>(1);
			builder.AddComponentParameter(2, nameof(UsageOutcomeBar.Tally), UsageSamples.Tally(answered: 2573, after: 43));
			builder.AddComponentParameter(3, nameof(UsageOutcomeBar.Width), 100.0);
			builder.AddComponentParameter(4, nameof(UsageOutcomeBar.Title), (RenderFragment)(title => title.AddContent(0, "TMDb")));
			builder.CloseComponent();
		}
	}
}
