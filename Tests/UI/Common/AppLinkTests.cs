using BlazorBlueprint.Icons.Lucide.Components;
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
	public async Task Link_WhenExternal_OpensInANewTabWithTheArrowInABox()
	{
		var cut = Render<AppLink>(parameters =>
			parameters.Add(link => link.Href, "https://github.com/daerup/Sammlarr").Add(link => link.External, true).AddChildContent("GitHub")
		);

		var link = cut.Find("a");
		var boxed = Render<LucideIcon>(parameters => parameters.Add(icon => icon.Name, "external-link").Add(icon => icon.Size, 12));
		await Assert.That(link.GetAttribute("target")).IsEqualTo("_blank");
		await Assert.That(link.GetAttribute("rel")).IsEqualTo("noopener noreferrer");
		await Assert.That(cut.Find(".app-link-arrow").InnerHtml).IsEqualTo(boxed.Find("svg").InnerHtml);
	}

	[Test]
	public async Task Link_WithAColour_TakesItOverTheForeground()
	{
		var cut = Render<AppLink>(parameters =>
			parameters.Add(link => link.Href, "/settings?section=providers").Add(link => link.Color, "inherit").AddChildContent("Settings")
		);

		await Assert.That(cut.Find("a").GetAttribute("style")).IsEqualTo("color: inherit");
	}

	[Test]
	public async Task Link_WhenMuted_MarksItMutedWithoutPinningAColour()
	{
		var cut = Render<AppLink>(parameters => parameters.Add(link => link.Href, "/feeds").Add(link => link.Muted, true).AddChildContent("Feeds"));

		var link = cut.Find("a");
		await Assert.That(link.ClassList).Contains("is-muted");
		await Assert.That(link.HasAttribute("style")).IsFalse();
	}
}
