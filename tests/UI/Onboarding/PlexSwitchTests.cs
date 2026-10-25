using BlazorBlueprint.Components;
using Bunit;
using Contracts.Identity;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Onboarding.Components;

namespace UI.Tests.Onboarding;

public class PlexSwitchTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		Services.AddSingleton<ICurrentUser>(new TestCurrentUser { DisplayName = "Lena" });
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Switch_SaysHowToChangeTheProfileInPlexAndSignsInAgainBackToSetup()
	{
		var cut = Render<PlexSwitch>();

		await Assert
			.That(cut.FindAll("[data-switch-steps] li").Select(step => step.TextContent.Words()))
			.IsEquivalentTo([
				"Open plex.tv in this browser.",
				"In the account menu, choose Switch User and pick your own profile.",
				"Come back here and sign in with Plex again.",
			]);
		var form = cut.Find("form");
		await Assert.That((form.GetAttribute("method"), form.GetAttribute("action"))).IsEqualTo(("get", "/auth/plex/login"));
		await Assert.That(form.QuerySelector("input[name=returnUrl]")?.GetAttribute("value")).IsEqualTo("/setup");
		await Assert.That(form.QuerySelector("input[name=swap]")).IsNull();
	}

	[Test]
	public async Task Switch_WhenSwappingTheAdmin_MarksTheSignInAsASwap()
	{
		var cut = Render<PlexSwitch>(parameters => parameters.Add(view => view.Replacing, true));

		await Assert.That(cut.Find("form input[name=swap]").GetAttribute("value")).IsEqualTo("true");
		await Assert.That(cut.FindAll("[data-replacing]")).HasSingleItem();
	}
}
