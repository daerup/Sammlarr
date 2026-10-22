using BlazorBlueprint.Components;
using BlazorBlueprint.Icons.Lucide.Components;
using Bunit;
using Microsoft.JSInterop;
using UI.Features.Settings.Components.Providers.Accounts;

namespace UI.Tests.Settings.Providers.Accounts;

public class SetupValueTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Copy_PutsTheValueOnTheClipboardAndTurnsIntoACheck()
	{
		var cut = RenderValue("http://localhost/");

		await cut.Find("button[aria-label='Copy App Redirect URL']").ClickAsync(new());

		await Assert.That(JSInterop.VerifyInvoke("navigator.clipboard.writeText").Arguments.Single()).IsEqualTo("http://localhost/");
		await Assert.That(cut.FindComponent<LucideIcon>().Instance.Name).IsEqualTo("check");
	}

	[Test]
	public async Task Copy_WhereTheBrowserRefuses_SaysToCopyItByHand()
	{
		JSInterop.SetupVoid("navigator.clipboard.writeText", "http://localhost/").SetException(new JSException("Clipboard needs a secure context"));
		var cut = RenderValue("http://localhost/");

		await cut.Find("button").ClickAsync(new());

		await Assert.That(cut.Find("[role=status]").TextContent).Contains("copy it by hand");
	}

	[Test]
	public async Task Render_ForAValueToTypeYourself_OffersNoCopy()
	{
		var cut = Render<SetupValue>(parameters =>
			parameters.Add(value => value.Name, "Name / Company Name").Add(value => value.Value, "your name").Add(value => value.Copy, false)
		);

		await Assert.That(cut.FindAll("button")).IsEmpty();
		await Assert.That(cut.Find("[data-setup-value]").TextContent.Trim()).IsEqualTo("your name");
	}

	private IRenderedComponent<SetupValue> RenderValue(string value) =>
		Render<SetupValue>(parameters => parameters.Add(setup => setup.Name, "App Redirect URL").Add(setup => setup.Value, value));
}
