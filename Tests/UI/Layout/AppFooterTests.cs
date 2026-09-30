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
	public async Task Credit_SaysMadeWithLove()
	{
		var cut = Render<AppFooter>(parameters => parameters.Add(footer => footer.Version, new AppVersion("1.4.0")));

		var words = cut.Find("footer").TextContent.Split(default(char[]), StringSplitOptions.RemoveEmptyEntries);
		await Assert.That(string.Join(' ', words)).EndsWith("Made with love");
	}
}
