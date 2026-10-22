using BlazorBlueprint.Components;
using Bunit;
using UI.Features.Settings.Components.Providers.Accounts;

namespace UI.Tests.Settings.Providers.Accounts;

public class KeySetupTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Trigger_OpensTheNumberedStepsThatStartClosed()
	{
		var cut = RenderSetup();
		await Assert.That(cut.Find("[data-key-setup] button").GetAttribute("aria-expanded")).IsEqualTo("false");

		await cut.Find("[data-key-setup] button").ClickAsync(new());

		await Assert.That(cut.Find("[data-key-setup] button").GetAttribute("aria-expanded")).IsEqualTo("true");
		await Assert.That(cut.Find("ol li").TextContent).Contains("1").And.Contains("Sign in at myanimelist.net");
	}

	private IRenderedComponent<KeySetup> RenderSetup() =>
		Render<KeySetup>(parameters =>
			parameters
				.Add(setup => setup.Title, "How to get a client ID")
				.Add(setup => setup.Time, "about 3 minutes")
				.AddChildContent<KeySetupStep>(step => step.Add(s => s.Number, 1).AddChildContent("Sign in at myanimelist.net"))
		);
}
