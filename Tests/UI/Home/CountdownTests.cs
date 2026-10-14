using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Home.Components;

namespace UI.Tests.Home;

public class CountdownTests : BunitContext
{
	private static readonly DateTimeOffset Start = new(2026, 9, 30, 14, 0, 0, TimeSpan.Zero);

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Text_AsTimePasses_TicksDownWithoutBeingRenderedAgain()
	{
		var now = Start;
		UseClock(() => now);
		var cut = Render<Harness>();
		var atFirst = cut.Find(".countdown").TextContent;
		now = Start.AddSeconds(90);

		cut.WaitForState(() => cut.Find(".countdown").TextContent == "in under a minute", TimeSpan.FromSeconds(3));
		await Assert.That(atFirst).IsEqualTo("in 2 min");
	}

	[Test]
	public async Task Tooltip_OnHover_ShowsTheTimeLeftToTheSecondAndKeepsTicking()
	{
		var now = Start;
		UseClock(() => now);
		var cut = Render<Harness>();

		await cut.Find(".countdown").ParentElement!.TriggerEventAsync("onmouseenter", new MouseEventArgs());
		var atFirst = Tooltip(cut);
		now = Start.AddSeconds(5);

		cut.WaitForState(() => Tooltip(cut) == "in 1 min 55 s", TimeSpan.FromSeconds(3));
		await Assert.That(atFirst).IsEqualTo("in 2 min 0 s");
	}

	private void UseClock(Func<DateTimeOffset> now)
	{
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(now);
		Services.AddSingleton(clock.Object);
	}

	private static string Tooltip(IRenderedComponent<Harness> cut) =>
		cut.WaitForElement("[data-portal-content][data-state=open] [role=tooltip]").TextContent.Trim();

	private sealed class Harness : ComponentBase
	{
		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<Countdown>(1);
			builder.AddComponentParameter(2, nameof(Countdown.At), Start.AddMinutes(2));
			builder.CloseComponent();
		}
	}
}
