using Bunit;
using UI.Shared.Common;

namespace UI.Tests.Common;

public class AppLinkTests : BunitContext
{
	[Test]
	public async Task Link_ByDefault_StaysInTheAppAndPointsTheDiagonalArrowAtWhereItGoes()
	{
		var cut = Render<AppLink>(parameters => parameters.Add(link => link.Href, "/watchlist").AddChildContent("12"));

		var link = cut.Find("a");
		await Assert.That(link.GetAttribute("href")).IsEqualTo("/watchlist");
		await Assert.That(link.HasAttribute("target")).IsFalse();
		await Assert.That(link.HasAttribute("style")).IsFalse();
		await Assert.That(cut.FindAll(".app-link-arrow")).IsNotEmpty();
	}

	[Test]
	public async Task Link_InsideASentence_CanDropTheArrow()
	{
		var cut = Render<AppLink>(parameters => parameters.Add(link => link.Href, "/feeds").Add(link => link.Arrow, false).AddChildContent("4"));

		await Assert.That(cut.FindAll(".app-link-arrow")).IsEmpty();
	}

	[Test]
	public async Task Link_WithAColour_TakesItOverTheForeground()
	{
		var cut = Render<AppLink>(parameters =>
			parameters.Add(link => link.Href, "/settings?section=providers").Add(link => link.Color, "inherit").AddChildContent("Settings")
		);

		await Assert.That(cut.Find("a").GetAttribute("style")).IsEqualTo("color: inherit");
	}
}
