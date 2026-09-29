using Bunit;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using UI.Shared.Branding;
using UI.Shared.Services.Branding;

namespace UI.Tests.Branding;

public class ProviderLogoTests : BunitContext
{
	[Before(Test)]
	public void Setup() => Services.AddSingleton<MediaProviderBrandingService>();

	[Test]
	public async Task Mark_ForAProvider_IsASquareImageAtTheGivenSize()
	{
		var cut = Render<ProviderLogo>(parameters => parameters.Add(logo => logo.Provider, MediaProvider.Tmdb).Add(logo => logo.Size, 24));

		var mark = cut.Find("img");
		await Assert.That(mark.GetAttribute("src")).IsEqualTo("_content/UI/providers/tmdb-square.svg");
		await Assert.That($"{mark.GetAttribute("width")}x{mark.GetAttribute("height")}").IsEqualTo("24x24");
	}

	[Test]
	public async Task Logo_WithALightAndDarkFile_RendersBothSoTheThemePicksOne()
	{
		var cut = Render<ProviderLogo>(parameters => parameters.Add(logo => logo.Provider, MediaProvider.Plex).Add(logo => logo.Form, ProviderLogoForm.Logo));

		var sources = cut.FindAll("img").Select(image => $"{image.ClassList.Contains("is-light")}|{image.GetAttribute("src")}");
		await Assert.That(sources).IsEquivalentTo(["True|_content/UI/providers/plex-logo-light.svg", "False|_content/UI/providers/plex-logo-dark.svg"]);
	}

	[Test]
	public async Task Logo_AtAGivenSize_HoldsThatHeightSoTheWidthFollowsTheArt()
	{
		var cut = Render<ProviderLogo>(parameters =>
			parameters.Add(logo => logo.Provider, MediaProvider.Plex).Add(logo => logo.Form, ProviderLogoForm.Logo).Add(logo => logo.Size, 22)
		);

		var sizes = cut.FindAll("img").Select(image => $"{image.GetAttribute("width")}|{image.GetAttribute("style")}");
		await Assert.That(sizes).IsEquivalentTo(["|height: 22px", "|height: 22px"]);
	}

	[Test]
	public async Task Logo_WhoseArtLeavesRoomBesideTheName_IsDrawnTallerSoItsLettersMatchTheOthers()
	{
		var cut = Render<ProviderLogo>(parameters =>
			parameters.Add(logo => logo.Provider, MediaProvider.Mdblist).Add(logo => logo.Form, ProviderLogoForm.Logo).Add(logo => logo.Size, 18)
		);

		await Assert.That(cut.Find("img").GetAttribute("style")).IsEqualTo("height: 22.5px");
	}

	[Test]
	public async Task Mark_OnItsOwn_IsNamedForAssistiveTechnology()
	{
		var cut = Render<ProviderLogo>(parameters => parameters.Add(logo => logo.Provider, MediaProvider.Imdb));

		await Assert.That(cut.Find("img").GetAttribute("alt")).IsEqualTo("IMDb");
	}

	[Test]
	public async Task Mark_NextToItsVisibleName_StaysSilent()
	{
		var cut = Render<ProviderLogo>(parameters => parameters.Add(logo => logo.Provider, MediaProvider.Tvdb).Add(logo => logo.Decorative, true));

		await Assert.That(cut.Find("img").GetAttribute("alt")).IsEqualTo(string.Empty);
	}
}
