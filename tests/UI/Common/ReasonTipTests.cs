using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using UI.Shared.Common;

namespace UI.Tests.Common;

public class ReasonTipTests : BunitContext
{
	private const string Reason = "Connect your Plex account to sync.";

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	private ToastService Toasts => Services.GetRequiredService<ToastService>();

	[Test]
	public async Task Desktop_ShowsTheReasonInATooltipWithTheUsualTiming()
	{
		var cut = Render<ReasonTip>(parameters =>
			parameters.Add(tip => tip.Reason, Reason).Add(tip => tip.TriggerClass, "inline-block").AddChildContent("<button disabled>Sync</button>")
		);

		var tooltip = cut.FindComponent<BbTooltip>().Instance;
		var trigger = cut.FindComponent<BbTooltipTrigger>().Instance;
		await Assert.That(Render(cut.FindComponent<BbTooltipContent>().Instance.ChildContent!).Markup).IsEqualTo(Reason);
		await Assert.That(tooltip.DelayDuration).IsEqualTo(300);
		await Assert.That(tooltip.Placement).IsEqualTo(PopoverPlacement.Top);
		await Assert.That(trigger.Focusable).IsFalse();
		await Assert.That(trigger.Class).IsEqualTo("inline-block");
	}

	[Test]
	public async Task Desktop_WithoutAReason_RendersNoTooltipContent()
	{
		var cut = Render<ReasonTip>(parameters => parameters.AddChildContent("<button>Sync</button>"));

		await Assert.That(cut.FindComponents<BbTooltipContent>()).IsEmpty();
	}

	[Test]
	public async Task Phone_TappingTheWrapper_ShowsTheReasonWithAnActionThatOpensTheLink()
	{
		var cut = this.RenderOnPhone<ReasonTip>(parameters =>
			parameters
				.Add(tip => tip.Reason, Reason)
				.Add(tip => tip.ActionText, "Settings")
				.Add(tip => tip.ActionHref, "/settings?section=providers&provider=plex")
				.AddChildContent("<button disabled>Sync</button>")
		);

		await cut.Component.Find(".reason-tip").ClickAsync();
		var toast = Toasts.Toasts.Single();
		toast.OnAction!();

		await Assert.That(toast.Description).IsEqualTo(Reason);
		await Assert.That(toast.ActionText).IsEqualTo("Settings");
		await Assert.That(Services.GetRequiredService<NavigationManager>().Uri).EndsWith("/settings?section=providers&provider=plex");
	}

	[Test]
	public async Task Phone_TappedAgain_KeepsASingleToast()
	{
		var cut = this.RenderOnPhone<ReasonTip>(parameters => parameters.Add(tip => tip.Reason, Reason).AddChildContent("<button disabled>Sync</button>"));

		await cut.Component.Find(".reason-tip").ClickAsync();
		await cut.Component.Find(".reason-tip").ClickAsync();

		await Assert.That(Toasts.Toasts).HasSingleItem();
	}

	[Test]
	public async Task Phone_WithoutAReason_StaysQuiet()
	{
		var cut = this.RenderOnPhone<ReasonTip>(parameters => parameters.AddChildContent("<button>Sync</button>"));

		await cut.Component.Find(".reason-tip").ClickAsync();

		await Assert.That(Toasts.Toasts).IsEmpty();
	}
}
