using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Domain.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using UI.Shared.Guards;

namespace UI.Tests.Guards;

public class ProtectedComponentTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	[Arguments(SinkKind.Plex, "Plex account")]
	[Arguments(SinkKind.Arr, "Radarr + Sonarr")]
	public async Task Tooltip_WhenTheSinkIsNotReady_NamesWhatIsMissingAndWhatItBlocks(SinkKind sink, string missing)
	{
		var cut = Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<ProtectedComponent>(1);
			builder.AddComponentParameter(2, nameof(ProtectedComponent.GuardState), () => false);
			builder.AddComponentParameter(3, nameof(ProtectedComponent.Sink), (SinkKind?)sink);
			builder.AddComponentParameter(4, nameof(ProtectedComponent.DeniedActionDescription), "sync your wantlist");
			builder.AddComponentParameter(
				5,
				nameof(ProtectedComponent.ChildContent),
				(RenderFragment)(content => content.AddMarkupContent(0, "<button>Sync</button>"))
			);
			builder.CloseComponent();
		});

		await cut.Find(".cursor-not-allowed").FirstElementChild!.TriggerEventAsync("onmouseenter", new MouseEventArgs());

		await Assert.That(cut.WaitForElement("[role=tooltip]").TextContent).Contains(missing).And.Contains("sync your wantlist");
	}
}
