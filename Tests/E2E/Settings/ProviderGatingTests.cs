using E2E.Tests.Feeds;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

public class ProviderGatingTests
{
	private const string Tmdb = "The Movie Database";

	private const string KeyInput = "input[placeholder='Enter API key']";

	[Test]
	public async Task ApiKey_WhenRemovedAndRestored_TogglesProviderAvailabilityInTheFeedEditor()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("providers"));
		var saved = await (await SettingsPage.OpenProviderAsync(page, Tmdb)).Locator(KeyInput).InputValueAsync();
		await Assert.That(saved).IsNotEmpty();

		await page.VisitAsync(Routes.CreateFeed);
		await Expect(FeedEditor.Option(page, "Provider", "TMDB")).ToBeEnabledAsync();

		await SettingsPage.VisitSectionAsync(page, "providers");
		var row = await SettingsPage.OpenProviderAsync(page, Tmdb);
		await DeleteKeyAsync(page, row);
		await Expect(row.Text("Not set")).ToBeVisibleAsync();

		await page.VisitAsync(Routes.CreateFeed);

		await Expect(FeedEditor.Option(page, "Provider", "TMDB")).ToBeDisabledAsync();
		await Expect(FeedEditor.Option(page, "Provider", "Mdblist")).ToBeEnabledAsync();

		await SettingsPage.VisitSectionAsync(page, "providers");
		await RestoreKeyAsync(await SettingsPage.OpenProviderAsync(page, Tmdb), saved);
		await page.VisitAsync(Routes.CreateFeed);

		await Expect(FeedEditor.Option(page, "Provider", "TMDB")).ToBeEnabledAsync();
	}

	private static async Task DeleteKeyAsync(IPage page, ILocator row)
	{
		await row.Button("Delete key").ClickAsync();
		await Expect(page.Heading("Delete API key?")).ToBeVisibleAsync();
		await page.Button("Delete key").Last.ClickAsync();
	}

	private static async Task RestoreKeyAsync(ILocator row, string key)
	{
		await row.Locator(KeyInput).FillAsync(key);
		await row.Button("Test").ClickAsync();
		await row.Button("Save").ClickAsync();
		await Expect(row.Text("Connected")).ToBeVisibleAsync();
	}
}
