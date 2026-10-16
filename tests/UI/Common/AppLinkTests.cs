using Bunit;
using UI.Shared.Common;

namespace UI.Tests.Common;

public class AppLinkTests : BunitContext
{
	[Test]
	public async Task Link_ByDefault_StaysInTheAppAndPointsTheDiagonalArrowAtWhereItGoes()
	{
		var cut = Render<AppLink>(parameters => parameters.Add(link => link.Href, "/wantlist").AddChildContent("12"));

		var link = cut.Find("a");
		await Assert.That(link.GetAttribute("href")).IsEqualTo("/wantlist");
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
	public async Task Link_WhenExternal_OpensInANewTabWithoutLeakingTheOpener()
	{
		var cut = Render<AppLink>(parameters =>
			parameters.Add(link => link.Href, "https://github.com/daerup/Sammlarr").Add(link => link.External, true).AddChildContent("GitHub")
		);

		var link = cut.Find("a");
		await Assert.That(link.GetAttribute("target")).IsEqualTo("_blank");
		await Assert.That(link.GetAttribute("rel")).IsEqualTo("noopener noreferrer");
	}
}
