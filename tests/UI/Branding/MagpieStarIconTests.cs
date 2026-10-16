using Bunit;
using UI.Shared.Branding;

namespace UI.Tests.Branding;

public class MagpieStarIconTests : BunitContext
{
	[Test]
	public async Task Outline_DrawsTheStarsEdgeInTheTextColour()
	{
		var cut = Render<MagpieStarIcon>(parameters => parameters.Add(star => star.Variant, MagpieStarVariant.Outline));

		var path = cut.Find("path");
		await Assert.That(path.GetAttribute("fill")).IsEqualTo("none");
		await Assert.That(path.GetAttribute("stroke")).IsEqualTo("currentColor");
	}

	[Test]
	public async Task Filled_FillsTheStarInTheTextColour()
	{
		var cut = Render<MagpieStarIcon>(parameters => parameters.Add(star => star.Variant, MagpieStarVariant.Filled));

		await Assert.That(cut.Find("path").GetAttribute("fill")).IsEqualTo("currentColor");
		await Assert.That(cut.Find("path").HasAttribute("stroke")).IsFalse();
	}
}
