using Bunit;
using Domain.Providers;
using UI.Shared.Branding;

namespace UI.Tests.Branding;

public class ProviderLogoTests : BunitContext
{
	[Before(Test)]
	public void Setup() => Services.AddProviderBranding();

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

		await Assert
			.That(cut.FindAll("img").Select(image => image.GetAttribute("src")).OfType<string>())
			.IsEquivalentTo(["_content/UI/providers/plex-logo-light.svg", "_content/UI/providers/plex-logo-dark.svg"]);
	}

	[Test]
	[Arguments(MediaProvider.Tvdb, 22, "height: 22px")]
	[Arguments(MediaProvider.Mdblist, 18, "height: 22.5px")]
	[Arguments(MediaProvider.Plex, 18, "height: 20.7px")]
	public async Task Logo_AtAGivenSize_HoldsThatSizeScaledByItsArtAndLetsTheWidthFollow(MediaProvider provider, int size, string height)
	{
		var cut = Render<ProviderLogo>(parameters =>
			parameters.Add(logo => logo.Provider, provider).Add(logo => logo.Form, ProviderLogoForm.Logo).Add(logo => logo.Size, size)
		);

		var images = cut.FindAll("img");
		await Assert.That(images.Select(image => image.GetAttribute("style")).OfType<string>().Distinct(StringComparer.Ordinal)).IsEquivalentTo([height]);
		await Assert.That(images.Where(image => image.HasAttribute("width"))).IsEmpty();
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
