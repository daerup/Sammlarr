using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

public class SettingsSectionTests
{
	[Test]
	[Arguments("provider-settings", "Choose which provider powers the watchlist and OAuth flow.")]
	[Arguments("secrets", "Your TMDB v3 API key")]
	[Arguments("notifications", "Webhook or Pushover connections to deliver to.")]
	[Arguments("cache", "Id groups.")]
	[Arguments("diagnostics", "Live log stream")]
	[Arguments("users", "People with access to this Sammlarr instance.")]
	[Arguments("testing", "Fire a toast of the chosen kind")]
	public async Task Section_WhenSelectedByQueryAsAdmin_RendersThatSection(string slug, string expectedContent)
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();

		await page.VisitAsync($"/settings?section={slug}");

		await Expect(page.GetByText(expectedContent)).ToBeVisibleAsync();
	}

	[Test]
	[Arguments("Cache")]
	[Arguments("Diagnostics")]
	[Arguments("Users")]
	[Arguments("Testing")]
	public async Task AdminSection_WhenSignedInAsAdmin_IsOfferedInTheSectionNav(string label)
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();

		await page.VisitAsync("/settings");

		await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = label })).ToBeVisibleAsync();
	}

	[Test]
	public async Task Section_WhenSlugIsUnknown_FallsBackWithoutFailing()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();

		await page.VisitAsync("/settings?section=does-not-exist");

		await Assert.That(page.Url).DoesNotContain("/signin");
		await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Settings" })).ToBeVisibleAsync();
	}
}
