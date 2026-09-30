using BlazorBlueprint.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using UI.Shared.Guards;
using UI.Shared.Services.Branding;

namespace UI.Tests.Guards;

public class SignInRequiredTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton<MediaProviderBrandingService>();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Prompt_ForTheOnlyProvider_NamesItsAccount()
	{
		var cut = Render<SignInRequired>();

		await Assert.That(cut.Find("h2").TextContent).IsEqualTo("Sign in");
		await Assert.That(cut.Find("p").TextContent).IsEqualTo("Use your Plex account to continue.");
	}

	[Test]
	public async Task Button_ForEachProvider_ReadsSignInWithThenTheProvidersWordmark()
	{
		var cut = Render<SignInRequired>();

		var button = cut.Find("button.signin-button");
		var wordmarks = button.QuerySelectorAll("img").Select(logo => $"{logo.GetAttribute("alt")}|{logo.GetAttribute("src")}");
		await Assert.That(button.TextContent.Trim()).IsEqualTo("Sign in with");
		await Assert.That(wordmarks).IsEquivalentTo(["Plex|_content/UI/providers/plex-logo-light.svg", "Plex|_content/UI/providers/plex-logo-dark.svg"]);
	}

	[Test]
	public async Task Alert_WithAnError_ShowsTheMessage()
	{
		var cut = Render<SignInRequired>(parameters => parameters.Add(card => card.Error, "Registration is closed. Ask an admin for access."));

		await Assert.That(cut.Find(".alert-soft-destructive").TextContent.Trim()).IsEqualTo("Registration is closed. Ask an admin for access.");
	}
}
