using Microsoft.AspNetCore.Localization;

namespace Web.Tests.Localization;

public class BrowserLocalizationTests
{
	[Test]
	public async Task CreateOptions_DefaultsToEnglish()
	{
		var options = BrowserLocalization.CreateOptions();

		await Assert.That(options.DefaultRequestCulture.Culture.Name).IsEqualTo("en-US");
	}

	[Test]
	public async Task CreateOptions_NegotiatesCultureFromBrowserHeader()
	{
		var options = BrowserLocalization.CreateOptions();

		var negotiatesFromBrowser = options.RequestCultureProviders.Any(provider => provider is AcceptLanguageHeaderRequestCultureProvider);

		await Assert.That(negotiatesFromBrowser).IsTrue();
	}

	[Test]
	[Arguments("en-US")]
	[Arguments("de-CH")]
	[Arguments("de-DE")]
	public async Task CreateOptions_SupportsBrowserCulture(string culture)
	{
		var options = BrowserLocalization.CreateOptions();

		var supported = options.SupportedCultures!.Any(candidate => candidate.Name == culture);

		await Assert.That(supported).IsTrue();
	}
}
