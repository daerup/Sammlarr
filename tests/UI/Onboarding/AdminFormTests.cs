using BlazorBlueprint.Components;
using Bunit;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.Options;
using UI.Features.Onboarding.Components;

namespace UI.Tests.Onboarding;

public class AdminFormTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(new AccountsOptions());
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Form_BeforeAWayIsPicked_LeavesBothCardsUncheckedAndReachableSubmitsNothingAndSaysThePickIsFinal()
	{
		var cut = Render<AdminForm>();

		var radios = cut.FindAll("[role=radio]");
		await Assert.That(radios.Select(radio => radio.GetAttribute("aria-checked")).OfType<string>()).IsEquivalentTo(["false", "false"]);
		await Assert.That(radios.Select(radio => radio.GetAttribute("tabindex")).OfType<string>()).IsEquivalentTo(["0", "-1"]);
		await Assert.That(cut.FindAll("input[name=username]")).IsEmpty();
		await Assert.That(cut.FindAll("button[formaction]")).IsEmpty();
		await Assert.That(cut.Find("button[type=submit]").HasAttribute("disabled")).IsTrue();
		await Assert.That(cut.Find("[role=radiogroup]").GetAttribute("aria-describedby")).IsEqualTo("signin-final");
		await Assert.That(cut.FindAll("#signin-final")).HasSingleItem();
	}

	[Test]
	public async Task Form_WithPlexPicked_OffersOnlyThePlexSignInPostingToItsOwnEndpoint()
	{
		var cut = Render<AdminForm>(parameters => parameters.Add(form => form.Picked, AuthProvider.Plex));

		await Assert.That(cut.Find("button[formaction]").GetAttribute("formaction")).IsEqualTo("/setup/plex");
		await Assert.That(cut.FindAll("input[name=username]")).IsEmpty();
	}

	[Test]
	public async Task Password_WithAConfiguredMinimum_AsksForThatManyCharacters()
	{
		Services.AddSingleton(new AccountsOptions { MinimumPasswordLength = 5 });

		var cut = Render<AdminForm>(parameters => parameters.Add(form => form.Picked, AuthProvider.Local));

		await Assert.That(cut.Find("input[name=password]").GetAttribute("placeholder")).IsEqualTo("At least 5 characters");
	}

	[Test]
	public async Task Form_WithSammlarrAccountsPicked_PostsTheAdminToSetupWithBothPasswordsMasked()
	{
		var cut = Render<AdminForm>(parameters => parameters.Add(form => form.Picked, AuthProvider.Local));

		await Assert.That(cut.Find("form").GetAttribute("action")).IsEqualTo("/setup/admin");
		await Assert
			.That(cut.FindAll("input[name]").Select(input => input.GetAttribute("name")).OfType<string>())
			.IsEquivalentTo(["code", "username", "password", "confirmation"]);
		await Assert
			.That(cut.FindAll("input[type=password]").Select(input => input.GetAttribute("name")).OfType<string>())
			.IsEquivalentTo(["password", "confirmation"]);
		await Assert.That(cut.FindAll("button[formaction]")).IsEmpty();
	}

	[Test]
	public async Task Card_WhenClicked_ShowsWhatItsWayNeeds()
	{
		var cut = Render<AdminForm>();

		await cut.Find("#signin-local").ClickAsync();

		await Assert.That(cut.Find("#signin-local").GetAttribute("aria-checked")).IsEqualTo("true");
		await Assert.That(cut.FindAll("input[name=username]")).HasSingleItem();
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
	public async Task Form_WithAnError_ShowsIt()
	{
		var cut = Render<AdminForm>(parameters => parameters.Add(form => form.Error, "That is not the setup code."));

		await Assert.That(cut.Find("[role=alert]").TextContent).Contains("That is not the setup code.");
	}
}
