using BlazorBlueprint.Components;
using Bunit;
using Microsoft.AspNetCore.Components;
using UI.Shared.Layout;

namespace UI.Tests.Layout;

public class AuthLayoutTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Logo_WhileSignedOut_IsShownWithoutLinkingHome()
	{
		var cut = Render<AuthLayout>(parameters => parameters.Add(layout => layout.Body, (RenderFragment)(_ => { })));

		var logo = cut.Find("svg.sammlarr-logo");
		await Assert.That(logo.Closest("a")).IsNull();
	}
}
