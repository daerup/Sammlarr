using BlazorBlueprint.Components;
using Bunit;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Onboarding.Components;
using UI.Shared.Services.Branding;

namespace UI.Tests.Onboarding;

public class AdminFormTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton<MediaProviderBrandingService>();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Form_AtFirst_PicksNeitherWayAndSaysThePickIsFinal()
	{
		var cut = Render<AdminForm>();

		await Assert.That(cut.FindAll(".choice-card[data-picked]")).IsEmpty();
		await Assert.That(cut.FindAll("input[name=username]")).IsEmpty();
		await Assert.That(cut.FindAll("button[formaction]")).IsEmpty();
		await Assert.That(cut.Find("[data-final]").TextContent).Contains("You can't change this after setup.");
	}

	[Test]
	public async Task Form_WithPlexPicked_OffersOnlyThePlexSignInPostingToItsOwnEndpoint()
	{
		var cut = Render<AdminForm>(parameters => parameters.Add(form => form.Picked, AuthProvider.Plex));

		await Assert.That(cut.Find("button[formaction]").GetAttribute("formaction")).IsEqualTo("/setup/plex");
		await Assert.That(cut.FindAll("input[name=username]")).IsEmpty();
	}

	[Test]
	public async Task Form_WithSammlarrAccountsPicked_PostsTheAdminToSetupWithBothPasswordsInOneRow()
	{
		var cut = Render<AdminForm>(parameters => parameters.Add(form => form.Picked, AuthProvider.Local));

		await Assert.That(cut.Find("form").GetAttribute("action")).IsEqualTo("/setup/admin");
		await Assert.That(cut.FindAll("input[name]").Select(input => input.GetAttribute("name"))).IsEquivalentTo(["code", "username", "password", "confirmation"]);
		await Assert.That(cut.Find("[data-passwords]").QuerySelectorAll("input[type=password]").Length).IsEqualTo(2);
		await Assert.That(cut.FindAll("button[formaction]")).IsEmpty();
	}

	[Test]
	public async Task Card_WhenClicked_ShowsWhatItsWayNeeds()
	{
		var cut = Render<AdminForm>();

		await cut.Find("#signin-local").ClickAsync();

		await Assert.That(cut.Find(".choice-card[data-picked]").GetAttribute("for")).IsEqualTo("signin-local");
		await Assert.That(cut.FindAll("input[name=username]")).HasSingleItem();
	}

	[Test]
	public async Task Form_BeforeAWayIsPicked_LetsTheKeyboardReachTheCardsAndEnterSubmitNothing()
	{
		var cut = Render<AdminForm>();

		await Assert.That(cut.FindAll("[role=radio]").Select(radio => radio.GetAttribute("tabindex"))).IsEquivalentTo(["0", "-1"]);
		await Assert.That(cut.Find("button[type=submit]").HasAttribute("disabled")).IsTrue();
	}

	[Test]
	public async Task Card_SwitchedFromSammlarrAccountsToPlex_DropsTheAccountFields()
	{
		var cut = Render<AdminForm>(parameters => parameters.Add(form => form.Picked, AuthProvider.Local));

		await cut.Find("#signin-plex").ClickAsync();

		await Assert.That(cut.FindAll("input[name=username]")).IsEmpty();
		await Assert.That(cut.Find("button[formaction]").GetAttribute("formaction")).IsEqualTo("/setup/plex");
	}

	[Test]
	public async Task Form_SpansTheCardWithoutAWidthCap()
	{
		var cut = Render<AdminForm>();

		await Assert.That(cut.Find("form").ClassList.Any(name => name.StartsWith("max-w", StringComparison.Ordinal))).IsFalse();
	}

	[Test]
	public async Task Cards_EachCarryTheirOwnBrand()
	{
		var cut = Render<AdminForm>();

		var brands = cut.FindAll(".choice-card").Select(card => card.GetAttribute("style"));
		var plex = Services.GetRequiredService<MediaProviderBrandingService>().GetBrandingInfo(MediaProvider.Plex).BrandColor;
		await Assert.That(brands).IsEquivalentTo([$"--brand: {plex}", "--brand: var(--foreground)"]);
	}

	[Test]
	public async Task Form_WithAnError_ShowsIt()
	{
		var cut = Render<AdminForm>(parameters => parameters.Add(form => form.Error, "That is not the setup code."));

		await Assert.That(cut.Find("[role=alert]").TextContent).Contains("That is not the setup code.");
	}
}
