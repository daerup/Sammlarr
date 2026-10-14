using BlazorBlueprint.Components;
using Bunit;
using Domain.Providers;
using UI.Shared.Guards;

namespace UI.Tests.Guards;

public class SignInRequiredTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Prompt_ForPlex_NamesItsAccountAndReadsSignInWithThenThePlexWordmark()
	{
		var cut = Render<SignInRequired>(parameters => parameters.Add(card => card.Provider, AuthProvider.Plex));

		await Assert.That(cut.Find("h2").TextContent).IsEqualTo("Sign in");
		await Assert.That(cut.Find("p").TextContent).Contains("Plex");
		var button = cut.Find("button.signin-button");
		var wordmarks = button.QuerySelectorAll("img").Select(logo => $"{logo.GetAttribute("alt")}|{logo.GetAttribute("src")}");
		await Assert.That(button.TextContent.Trim()).IsEqualTo("Sign in with");
		await Assert.That(wordmarks).IsEquivalentTo(["Plex|_content/UI/providers/plex-logo-light.svg", "Plex|_content/UI/providers/plex-logo-dark.svg"]);
	}

	[Test]
	public async Task Form_OnASammlarrAccountsInstall_PostsTheUsernameAndPasswordWithoutAPlexButton()
	{
		var cut = Render<SignInRequired>(parameters => parameters.Add(card => card.Provider, AuthProvider.Local).Add(card => card.ReturnUrl, "/feeds"));

		var form = cut.Find("form");
		await Assert.That(cut.Find("p").TextContent).Contains("Sammlarr");
		await Assert.That(form.GetAttribute("action")).IsEqualTo("/auth/local");
		await Assert.That(form.QuerySelectorAll("input[name]").Select(input => input.GetAttribute("name") ?? string.Empty)).Contains("password");
		await Assert.That(form.QuerySelector("input[name=returnUrl]")?.GetAttribute("value")).IsEqualTo("/feeds");
		await Assert.That(cut.FindAll("button.signin-button")).IsEmpty();
	}

	[Test]
	public async Task Button_WhenPlexSignInIsClicked_SpinsAndDisablesItself()
	{
		var cut = Render<SignInRequired>(parameters => parameters.Add(card => card.Provider, AuthProvider.Plex));

		await cut.Find("button.signin-button").ClickAsync();

		var button = cut.Find("button.signin-button");
		await Assert.That(button.HasAttribute("disabled")).IsTrue();
		await Assert.That(button.TextContent.Trim()).IsEqualTo("Signing in…");
	}

	[Test]
	public async Task Form_ForSubmitting_HasAnOnSubmitHandlerAndABusyLabelOnItsButton()
	{
		var cut = Render<SignInRequired>(parameters => parameters.Add(card => card.Provider, AuthProvider.Local));

		var form = cut.Find("form");
		await Assert.That(form.HasAttribute("onsubmit")).IsTrue();
		await Assert.That(form.QuerySelector("button[type=submit] .signin-busy")?.TextContent.Trim()).IsEqualTo("Signing in…");
	}

	[Test]
	public async Task Alert_WithAnError_ShowsTheMessage()
	{
		var cut = Render<SignInRequired>(parameters =>
			parameters.Add(card => card.Provider, AuthProvider.Plex).Add(card => card.Error, "Registration is closed. Ask an admin for access.")
		);

		await Assert.That(cut.Find(".alert-soft-destructive").TextContent.Trim()).IsEqualTo("Registration is closed. Ask an admin for access.");
	}
}
