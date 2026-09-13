using E2E.Tests.Feeds;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

[NotInParallel]
public class ProviderGatingTests
{
	private const string TmdbCard = "The Movie Database";

	private const string KeyInput = "input[placeholder='Enter API key']";

	private static string _savedKey = string.Empty;

	[Test]
	public async Task ApiKeyRemoved_DisablesProviderInTheFeedEditor()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await SettingsPage.VisitSectionAsync(page, "secrets");
		_savedKey = await SettingsPage.Card(page, TmdbCard).Locator(KeyInput).InputValueAsync();
		await Assert.That(_savedKey).IsNotEmpty();

		await page.VisitAsync("/feeds/create");
		await Expect(FeedEditor.Option(page, "Provider", "TMDB")).ToBeEnabledAsync();

		await SettingsPage.VisitSectionAsync(page, "secrets");
		await DeleteKeyAsync(page);
		await Expect(SettingsPage.Card(page, TmdbCard).GetByText("Not configured")).ToBeVisibleAsync();

		await page.VisitAsync("/feeds/create");

		await Expect(FeedEditor.Option(page, "Provider", "TMDB")).ToBeDisabledAsync();
		await Expect(FeedEditor.Option(page, "Provider", "Mdblist")).ToBeEnabledAsync();
	}

	[Test]
	[DependsOn(nameof(ApiKeyRemoved_DisablesProviderInTheFeedEditor))]
	public async Task ApiKeyRestored_ReEnablesProvider()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await SettingsPage.VisitSectionAsync(page, "secrets");
		await RestoreKeyAsync(page);

		await page.VisitAsync("/feeds/create");

		await Expect(FeedEditor.Option(page, "Provider", "TMDB")).ToBeEnabledAsync();
	}

	[After(Class)]
	public static async Task LeaveTheKeyBehindAsync()
	{
		if (_savedKey.Length == 0)
		{
			return;
		}

		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await SettingsPage.VisitSectionAsync(page, "secrets");
		if (await SettingsPage.Card(page, TmdbCard).Locator(KeyInput).InputValueAsync() != _savedKey)
		{
			await RestoreKeyAsync(page);
		}
	}

	private static async Task DeleteKeyAsync(IPage page)
	{
		await SettingsPage.Button(SettingsPage.Card(page, TmdbCard), "Delete key").ClickAsync();
		await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Delete API key?" })).ToBeVisibleAsync();
		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Delete key", Exact = true }).Last.ClickAsync();
	}

	private static async Task RestoreKeyAsync(IPage page)
	{
		var card = SettingsPage.Card(page, TmdbCard);
		await card.Locator(KeyInput).FillAsync(_savedKey);
		await SettingsPage.Button(card, "Test").ClickAsync();
		await SettingsPage.Button(card, "Save").ClickAsync();
		await Expect(card.GetByText("Connected")).ToBeVisibleAsync();
	}
}
