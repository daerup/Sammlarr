using E2E.Tests.Feeds;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

public class ProviderGatingTests
{
	private const string TmdbCard = "The Movie Database";

	private const string KeyInput = "input[placeholder='Enter API key']";

	[Test]
	public async Task ApiKey_WhenRemovedAndRestored_TogglesProviderAvailabilityInTheFeedEditor()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("secrets"));
		var saved = await SettingsPage.Card(page, TmdbCard).Locator(KeyInput).InputValueAsync();
		await Assert.That(saved).IsNotEmpty();

		await page.VisitAsync(Routes.CreateFeed);
		await Expect(FeedEditor.Option(page, "Provider", "TMDB")).ToBeEnabledAsync();

		await SettingsPage.VisitSectionAsync(page, "secrets");
		await DeleteKeyAsync(page);
		await Expect(SettingsPage.Card(page, TmdbCard).Text("Not configured")).ToBeVisibleAsync();

		await page.VisitAsync(Routes.CreateFeed);

		await Expect(FeedEditor.Option(page, "Provider", "TMDB")).ToBeDisabledAsync();
		await Expect(FeedEditor.Option(page, "Provider", "Mdblist")).ToBeEnabledAsync();

		await SettingsPage.VisitSectionAsync(page, "secrets");
		await RestoreKeyAsync(page, saved);
		await page.VisitAsync(Routes.CreateFeed);

		await Expect(FeedEditor.Option(page, "Provider", "TMDB")).ToBeEnabledAsync();
	}

	private static async Task DeleteKeyAsync(IPage page)
	{
		await SettingsPage.Card(page, TmdbCard).Button("Delete key").ClickAsync();
		await Expect(page.Heading("Delete API key?")).ToBeVisibleAsync();
		await page.Button("Delete key").Last.ClickAsync();
	}

	private static async Task RestoreKeyAsync(IPage page, string key)
	{
		var card = SettingsPage.Card(page, TmdbCard);
		await card.Locator(KeyInput).FillAsync(key);
		await card.Button("Test").ClickAsync();
		await card.Button("Save").ClickAsync();
		await Expect(card.Text("Connected")).ToBeVisibleAsync();
	}
}
