using BlazorBlueprint.Components;
using Bunit;
using Contracts.Identity;
using Microsoft.Extensions.DependencyInjection;
using UI.Shared.Identity;
using UI.Shared.Layout;

namespace UI.Tests.Layout;

public class AuthLayoutTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddScoped(_ => new CurrentUserAccessor(new CurrentUserOverride()));
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Logo_WhileSignedOut_IsShownWithoutLinkingHome()
	{
		var cut = Render<AuthLayout>(parameters => parameters.Add(layout => layout.Body, _ => { }));

		var logo = cut.Find("svg.sammlarr-logo");
		await Assert.That(logo.Closest("a")).IsNull();
	}
}
