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
	public async Task HomeLink_Always_IsNamedSammlarrHomeAndCarriesTheLogo()
	{
		var cut = Render<AuthLayout>(parameters => parameters.Add(layout => layout.Body, (RenderFragment)(_ => { })));

		var link = cut.Find("a[href='/']");
		await Assert.That(link.GetAttribute("aria-label")).IsEqualTo("Sammlarr home");
		await Assert.That(link.QuerySelector("svg.sammlarr-logo")).IsNotNull();
	}
}
