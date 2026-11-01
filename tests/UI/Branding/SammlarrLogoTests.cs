using System.Globalization;
using Bunit;
using UI.Shared.Branding;

namespace UI.Tests.Branding;

public class SammlarrLogoTests : BunitContext
{
	[Test]
	public async Task Logo_Always_IsHiddenFromAssistiveTechnology()
	{
		var cut = Render<SammlarrLogo>();

		var logo = cut.Find("svg.sammlarr-logo");
		await Assert.That(logo.GetAttribute("aria-hidden")).IsEqualTo("true");
		await Assert.That(logo.GetAttribute("focusable")).IsEqualTo("false");
	}

	[Test]
	public async Task Magpie_WithASize_IsDrawnSquareAtThatSize()
	{
		var cut = Render<SammlarrLogo>(parameters => parameters.Add(logo => logo.Size, 64));

		var layers = cut.FindAll(".sammlarr-mark");
		await Assert.That(layers.Select(layer => $"{layer.GetAttribute("width")}x{layer.GetAttribute("height")}").Distinct()).IsEquivalentTo(["64x64"]);
	}

	[Test]
	public async Task Logo_WithALargerWord_GrowsWider()
	{
		var small = Render<SammlarrLogo>(parameters => parameters.Add(logo => logo.WordSize, 17));
		var large = Render<SammlarrLogo>(parameters => parameters.Add(logo => logo.WordSize, 25));

		var width = (IRenderedComponent<SammlarrLogo> cut) =>
			double.Parse(cut.Find("svg.sammlarr-logo").GetAttribute("width") ?? string.Empty, CultureInfo.InvariantCulture);
		await Assert.That(width(large)).IsGreaterThan(width(small));
	}

	[Test]
	public async Task Logo_UnderACommaDecimalCulture_StillWritesDotDecimals()
	{
		CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

		var cut = Render<SammlarrLogo>();

		await Assert.That(cut.Find("svg.sammlarr-logo").GetAttribute("viewBox")).DoesNotContain(",");
		await Assert.That(cut.Find("g[transform]").GetAttribute("transform")).DoesNotContain(",");
	}

	[Test]
	public async Task Star_FillsTheStarInTheTextColourWithoutTheWord()
	{
		var cut = Render<SammlarrLogo>(parameters => parameters.Add(logo => logo.Form, SammlarrLogoForm.Star).Add(logo => logo.Size, 13));

		await Assert.That(cut.Find("path").GetAttribute("fill")).IsEqualTo("currentColor");
		await Assert.That(cut.Find("path").HasAttribute("stroke")).IsFalse();
		await Assert.That(cut.Find("svg.magpie-star").GetAttribute("width")).IsEqualTo("13");
		await Assert.That(cut.FindAll(".sammlarr-word")).IsEmpty();
	}

	[Test]
	public async Task StarOutline_DrawsTheStarsEdgeInTheTextColour()
	{
		var cut = Render<SammlarrLogo>(parameters => parameters.Add(logo => logo.Form, SammlarrLogoForm.StarOutline).Add(logo => logo.Size, 16));

		var path = cut.Find("svg.magpie-star path");
		await Assert.That(path.GetAttribute("fill")).IsEqualTo("none");
		await Assert.That(path.GetAttribute("stroke")).IsEqualTo("currentColor");
	}
}
