using BlazorBlueprint.Components;
using Bunit;
using UI.Shared.Layout;
using UI.Shared.Services;

namespace UI.Tests.Layout;

public class AppFooterTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Version_LinksToTheAboutSection()
	{
		var cut = Render<AppFooter>(parameters => parameters.Add(footer => footer.Version, new AppVersion("0.0.0-alpha.0.584")));

		var link = cut.Find("footer a");
		await Assert.That(link.GetAttribute("href")).IsEqualTo("/settings?section=about");
		await Assert.That(link.TextContent.Trim()).IsEqualTo("0.0.0-alpha.0.584");
	}

	[Test]
	public async Task Version_WhenANewerReleaseIsOut_CarriesADotAndSaysSo()
	{
		var update = new AppUpdate("1.5.0", new Uri("https://github.com/daerup/Sammlarr/releases/tag/v1.5.0"));

		var cut = Render<AppFooter>(parameters => parameters.Add(footer => footer.Version, new AppVersion("1.4.0")).Add(footer => footer.Update, Some.Of(update)));

		await Assert.That(cut.FindAll("[data-testid=footer-update-dot]")).HasSingleItem();
		await Assert.That(cut.Find("footer a").GetAttribute("title")).IsEqualTo("Sammlarr 1.5.0 is out");
	}

	[Test]
	public async Task Version_WhileUpToDate_CarriesNoDot()
	{
		var cut = Render<AppFooter>(parameters => parameters.Add(footer => footer.Version, new AppVersion("1.4.0")));

		await Assert.That(cut.FindAll("[data-testid=footer-update-dot]")).IsEmpty();
	}

	[Test]
	public async Task Footer_HoldsOnlyTheVersion()
	{
		var cut = Render<AppFooter>(parameters => parameters.Add(footer => footer.Version, new AppVersion("1.4.0")));

		await Assert.That(cut.Find("footer").TextContent.Trim()).IsEqualTo("1.4.0");
	}
}
