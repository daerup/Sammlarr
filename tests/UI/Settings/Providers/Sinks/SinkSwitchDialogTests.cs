using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts.Wantlist;
using Domain.Providers;
using Microsoft.AspNetCore.Components;
using UI.Features.Settings.Components.Providers.Sinks;

namespace UI.Tests.Settings.Providers.Sinks;

public class SinkSwitchDialogTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	[Arguments(30, 12, "30 titles are ready, 12 still need a lookup during the switch (any not found stay unmatched).")]
	[Arguments(1, 1, "1 title is ready, 1 still needs a lookup during the switch (any not found stay unmatched).")]
	[Arguments(42, 0, "42 titles are ready.")]
	public async Task Dialog_ForASwitch_SaysHowManyTitlesAreReadyAndHowManyStillNeedALookup(int ready, int toLookUp, string expected)
	{
		var cut = RenderDialog(SinkKind.Arr, new SinkSwitchPreview(ready, toLookUp), []);

		await Assert.That(cut.WaitForElement("[data-testid=switch-readiness]").TextContent).IsEqualTo(expected);
		await Assert.That(cut.FindAll("[data-testid=switch-without-plex]")).IsEmpty();
	}

	[Test]
	public async Task Dialog_ForASwitch_CountsEveryonesTitlesFromThePreview()
	{
		var cut = RenderDialog(SinkKind.Arr, new SinkSwitchPreview(30, 12), []);

		await Assert.That(cut.WaitForElement("[role=dialog]").TextContent).Contains("(3 people, 42 titles)");
	}

	[Test]
	public async Task Dialog_ForAnEmptyWantlist_SaysNothingAboutReadiness()
	{
		var cut = RenderDialog(SinkKind.Arr, new SinkSwitchPreview(0, 0), []);

		cut.WaitForElement("[data-testid=switch-preview]");
		await Assert.That(cut.FindAll("[data-testid=switch-readiness]")).IsEmpty();
	}

	[Test]
	[Arguments(new[] { "Ada", "Bob", "Cy" }, "Ada, Bob, and Cy have no Plex connected, so their wantlists wait until they connect.")]
	[Arguments(new[] { "Ada" }, "Ada has no Plex connected, so their wantlist waits until they connect.")]
	public async Task Dialog_ForASwitchToPlex_NamesThePeopleWithoutPlexWhoseWantlistsWait(string[] withoutPlex, string expected)
	{
		var cut = RenderDialog(SinkKind.Plex, new SinkSwitchPreview(1, 0), withoutPlex);

		await Assert.That(cut.WaitForElement("[data-testid=switch-without-plex]").TextContent).IsEqualTo(expected);
	}

	private IRenderedComponent<IComponent> RenderDialog(SinkKind target, SinkSwitchPreview preview, IReadOnlyList<string> withoutPlex)
	{
		return Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<SinkSwitchDialog>(1);
			builder.AddComponentParameter(2, nameof(SinkSwitchDialog.Open), true);
			builder.AddComponentParameter(3, nameof(SinkSwitchDialog.OpenChanged), EventCallback.Factory.Create<bool>(this, () => { }));
			builder.AddComponentParameter(4, nameof(SinkSwitchDialog.Target), target);
			builder.AddComponentParameter(5, nameof(SinkSwitchDialog.People), 3);
			builder.AddComponentParameter(6, nameof(SinkSwitchDialog.Preview), preview);
			builder.AddComponentParameter(7, nameof(SinkSwitchDialog.WithoutPlex), withoutPlex);
			builder.CloseComponent();
		});
	}
}
