using BlazorBlueprint.Components;
using Bunit;
using Domain.Providers;
using Microsoft.AspNetCore.Components;
using UI.Shared.Guards;

namespace UI.Tests.Guards;

public class ProtectedSectionTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Fallback_ForAMissingKey_AsksForThatKeyAndSaysWhy()
	{
		var cut = Render<ProtectedSection>(parameters =>
			parameters
				.Add(section => section.GuardState, () => false)
				.Add(section => section.ApiProvider, ApiKeyProvider.Tmdb)
				.Add(section => section.ApiKeyReason, "Browse shows what is trending on TMDB.")
				.Add(section => section.FallbackAction, EventCallback.Factory.Create(this, () => { }))
				.Add(section => section.ChildContent, _ => { })
		);

		await Assert.That(cut.Find("h3").TextContent).IsEqualTo("Add your TMDB key");
		await Assert.That(cut.Find("p").TextContent).IsEqualTo("Browse shows what is trending on TMDB.");
		await Assert.That(cut.Find("button").TextContent.Trim()).IsEqualTo("Add your TMDB key");
	}
}
