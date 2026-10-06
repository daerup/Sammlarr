using BlazorBlueprint.Components;
using Bunit;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Onboarding.Components;
using UI.Shared.Services.Branding;

namespace UI.Tests.Onboarding;

public class SinkCardsTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Cards_EachCarryTheirSinksColour()
	{
		var cut = Render<SinkCards>(parameters => parameters.Add(cards => cards.Picked, SinkKind.Plex));

		var branding = Services.GetRequiredService<MediaProviderBrandingService>();
		await Assert
			.That(cut.FindAll(".choice-card").Select(card => card.GetAttribute("style")).OfType<string>())
			.IsEquivalentTo([$"--brand: {branding.Color(SinkKind.Plex.Service)}", $"--brand: {branding.Color(SinkKind.Arr.Service)}"]);
	}

	[Test]
	public async Task Cards_MarkOnlyThePickedSink()
	{
		var cut = Render<SinkCards>(parameters => parameters.Add(cards => cards.Picked, SinkKind.Arr));

		await Assert.That(cut.Find(".choice-card[data-picked]").GetAttribute("for")).IsEqualTo("sink-Arr");
		await Assert.That(cut.FindAll("[role=radio]").Select(radio => radio.GetAttribute("tabindex")).OfType<string>()).IsEquivalentTo(["-1", "0"]);
	}
}
