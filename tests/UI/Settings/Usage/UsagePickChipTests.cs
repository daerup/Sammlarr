using BlazorBlueprint.Components;
using Bunit;
using Domain.Providers;
using UI.Features.Settings.Components.Usage;

namespace UI.Tests.Settings.Usage;

public class UsagePickChipTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_AService_ShowsItsName()
	{
		var cut = Render<UsagePickChip>(parameters => parameters.Add(chip => chip.Service, RemoteService.Mdblist));

		await Assert.That(cut.Find("[data-testid=usage-pick-chip]").TextContent.Trim()).IsEqualTo("MDBList");
	}

	[Test]
	public async Task Clear_PressedAsAButton_ShowsEveryService()
	{
		var cleared = false;
		var cut = Render<UsagePickChip>(parameters => parameters.Add(chip => chip.Service, RemoteService.Tmdb).Add(chip => chip.OnClear, () => cleared = true));

		var clear = cut.Find("button[aria-label='Show every service']");
		await clear.ClickAsync();

		await Assert.That(cleared).IsTrue();
		await Assert.That(clear.GetAttribute("tabindex")).IsNotEqualTo("-1");
	}
}
