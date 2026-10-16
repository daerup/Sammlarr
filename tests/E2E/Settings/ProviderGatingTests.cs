using E2E.Tests.Feeds;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

public class ProviderGatingTests
{
	private const string Mdblist = "Movie Database List";

	private const string KeyInput = "input[placeholder='Enter API key']";

	private const string Disabled = "data-disabled";

	[Test]
	public async Task ApiKey_WhenRemovedAndRestored_TogglesTheProviderInTheMapAndTheFeedEditor()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("providers"));
		var saved = await (await SettingsPage.OpenProviderAsync(page, Mdblist)).Locator(KeyInput).InputValueAsync();
		await Assert.That(saved).IsNotEmpty();

		await page.VisitAsync(Routes.CreateFeed);
		await Expect(await FeedEditor.ProviderOptionAsync(page, "MDBList")).Not.ToHaveAttributeAsync(Disabled, "true");

		await SettingsPage.VisitSectionAsync(page, "providers");
		var row = await SettingsPage.OpenProviderAsync(page, Mdblist);
		await DeleteKeyAsync(page, row);
		await Expect(row.Text("Not set")).ToBeVisibleAsync();
		await Expect(SettingsPage.MapSource(page, "MDBList")).ToContainTextAsync("No key yet");

		await page.VisitAsync(Routes.CreateFeed);

		var mdblist = await FeedEditor.ProviderOptionAsync(page, "MDBList");
		await Expect(mdblist).ToHaveAttributeAsync(Disabled, "true");
		await Expect(mdblist).ToContainTextAsync("No API key");

		await SettingsPage.VisitSectionAsync(page, "providers");
		await RestoreKeyAsync(await SettingsPage.OpenProviderAsync(page, Mdblist), saved);
		await Expect(SettingsPage.MapSource(page, "MDBList")).ToContainTextAsync("Your key is set");
		await page.VisitAsync(Routes.CreateFeed);

		await Expect(await FeedEditor.ProviderOptionAsync(page, "MDBList")).Not.ToHaveAttributeAsync(Disabled, "true");
	}

	private static async Task DeleteKeyAsync(IPage page, ILocator row)
	{
		await row.Button("Delete key").ClickAsync();
		await Expect(page.Heading("Delete API key?")).ToBeVisibleAsync();
		await page.Button("Delete key").Last.ClickAsync();
	}

	private static async Task RestoreKeyAsync(ILocator row, string key)
	{
		await row.Locator(KeyInput).EnterAsync(key);
		await row.Button("Test").ClickAsync();
		await row.Button("Save").ClickAsync();
		await Expect(row.Text("Connected")).ToBeVisibleAsync();
	}
}
