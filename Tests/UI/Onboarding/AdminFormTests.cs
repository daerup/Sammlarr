using BlazorBlueprint.Components;
using Bunit;
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
	public async Task Form_PostsTheLocalAdminToSetupAndThePlexChoiceToItsOwnEndpoint()
	{
		var cut = Render<AdminForm>();

		await Assert.That(cut.Find("form").GetAttribute("action")).IsEqualTo("/setup/admin");
		await Assert
			.That(cut.FindAll("input[name]").Select(input => input.GetAttribute("name")))
			.Contains("code")
			.And.Contains("username")
			.And.Contains("password")
			.And.Contains("confirmation");
		await Assert.That(cut.Find("button[formaction]").GetAttribute("formaction")).IsEqualTo("/setup/plex");
	}

	[Test]
	public async Task Form_WithAnError_ShowsIt()
	{
		var cut = Render<AdminForm>(parameters => parameters.Add(form => form.Error, "That is not the setup code."));

		await Assert.That(cut.Find("[role=alert]").TextContent).Contains("That is not the setup code.");
	}
}
