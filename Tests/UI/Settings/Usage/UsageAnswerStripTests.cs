using System.Globalization;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Domain.Providers;
using Domain.Usage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using UI.Features.Settings.Components.Usage;

namespace UI.Tests.Settings.Usage;

public class UsageAnswerStripTests : BunitContext
{
	private static readonly UsageTally Answers = new(new Dictionary<UsageOutcome, long>(), new Dictionary<int, long> { [100] = 90, [500] = 5 });

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_ATally_ShadesEachSquareByItsShareOfTheBusiest()
	{
		var cut = RenderStrip(Answers);

		var fills = cut.FindAll("[data-testid=usage-strip-square]").Select(square => square.GetAttribute("fill")).ToList();
		await Assert.That(fills).Count().IsEqualTo(15);
		await Assert.That(fills[5]).IsEqualTo(UsageView.Shade(90, 90));
		await Assert.That(fills[7]).IsEqualTo(UsageView.Shade(5, 90));
		await Assert.That(fills.Where(fill => fill == UsageView.Shade(0, 90))).Count().IsEqualTo(13);
	}

	[Test]
	public async Task Render_ATally_MarksTheMedianBeforeTheP95()
	{
		var cut = RenderStrip(Answers);

		var ticks = cut.FindAll("[data-testid=usage-strip-tick]").Select(tick => double.Parse(tick.GetAttribute("x")!, CultureInfo.InvariantCulture)).ToList();
		await Assert.That(ticks).Count().IsEqualTo(2);
		await Assert.That(ticks[0]).IsLessThan(ticks[1]);
	}

	[Test]
	public async Task Square_PointedAt_NamesItsRangeCallsAndShareWithTheMedianAndP95()
	{
		var cut = RenderStrip(Answers);

		await cut.FindAll("[data-testid=usage-strip-square]")[7].TriggerEventAsync("onmouseenter", new MouseEventArgs());
		await cut.Find("[data-testid=usage-strip]").ParentElement!.TriggerEventAsync("onmouseenter", new MouseEventArgs());

		var tip = cut.WaitForElement("[data-portal-content][data-state=open] [role=tooltip]").TextContent;
		await Assert.That(tip).Contains("TMDb").And.Contains("≤500 ms").And.Contains("5 calls").And.Contains("5.3%").And.Contains("median").And.Contains("p95");
		await Assert.That(cut.FindAll("[data-testid=usage-strip-square]")[7].GetAttribute("stroke")).IsEqualTo("var(--foreground)");
	}

	[Test]
	public async Task Render_WithoutTimedCalls_DrawsNothing()
	{
		var cut = RenderStrip(UsageTally.Empty);

		await Assert.That(cut.FindAll("[data-testid=usage-strip]")).IsEmpty();
	}

	[Test]
	public async Task Heading_InfoPointedAt_ExplainsTheSquaresOnASampleStrip()
	{
		var cut = RenderStrip(Answers);

		await cut.Find("[data-testid=usage-answer-time-info]").TriggerEventAsync("onmouseenter", new MouseEventArgs());

		var help = cut.WaitForElement("[data-portal-content][data-state=open] [data-testid=usage-answer-time-help]").TextContent;
		await Assert
			.That(help)
			.Contains("The stronger its shade, the more of the service's calls took that long.")
			.And.Contains("5 ms")
			.And.Contains(">10 s")
			.And.Contains("point at a square for its calls");
	}

	private IRenderedComponent<Harness> RenderStrip(UsageTally tally)
	{
		return Render<Harness>(parameters => parameters.Add(harness => harness.Tally, tally));
	}

	private sealed class Harness : ComponentBase
	{
		[Parameter]
		public required UsageTally Tally { get; set; }

		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<UsageAnswerTimeHead>(1);
			builder.CloseComponent();
			builder.OpenComponent<UsageAnswerStrip>(2);
			builder.AddComponentParameter(3, nameof(UsageAnswerStrip.Service), RemoteService.Tmdb);
			builder.AddComponentParameter(4, nameof(UsageAnswerStrip.Tally), Tally);
			builder.CloseComponent();
		}
	}
}
