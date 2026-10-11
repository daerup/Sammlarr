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
	public async Task Render_AService_ShowsItsLogoAndName()
	{
		var cut = Render<UsagePickChip>(parameters => parameters.Add(chip => chip.Service, RemoteService.Mdblist));

		var chip = cut.Find("[data-testid=usage-pick-chip]");
		await Assert.That(chip.TextContent.Trim()).IsEqualTo("MDBList");
		await Assert.That(chip.QuerySelector("img.provider-logo")!.GetAttribute("src")).Contains("mdblist");
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
