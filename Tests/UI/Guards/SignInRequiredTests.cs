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
		Services.AddScoped(_ => new MediaProviderBrandingService(string.Empty));
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
	public async Task Button_ForEachProvider_ShowsTheProviderMark()
	{
		var cut = Render<SignInRequired>();

		var buttons = cut.FindAll("button");
		await Assert.That(buttons.Select(button => button.QuerySelector(".provider-label")?.TextContent)).IsEquivalentTo(["Sign in with Plex"]);
		await Assert.That(buttons.All(button => button.QuerySelector(".provider-mark svg") is not null)).IsTrue();
	}

	[Test]
	public async Task Button_ForEachProvider_HidesTheMarkBehindItsVisibleName()
	{
		var cut = Render<SignInRequired>();

		await Assert.That(cut.FindAll("button .provider-mark").Select(mark => mark.GetAttribute("aria-hidden"))).IsEquivalentTo(["true"]);
	}

	[Test]
	public async Task Alert_WithAnError_ShowsTheMessage()
	{
		var cut = Render<SignInRequired>(parameters => parameters.Add(card => card.Error, "Registration is closed. Ask an admin for access."));

		await Assert.That(cut.Find(".alert-soft-destructive").TextContent.Trim()).IsEqualTo("Registration is closed. Ask an admin for access.");
	}
}
