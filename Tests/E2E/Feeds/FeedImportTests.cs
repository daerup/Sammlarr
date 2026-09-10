using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Feeds;

public class FeedImportTests
{
	private const string ListIdentifier = "2236";

	[Test]
	public async Task Save_WhenNameIsMissing_IsNotOffered()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");

		await SelectMdblistAsync(page);
		await page.Locator("#list-identifier").FillAsync(ListIdentifier);

		await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save", Exact = true })).ToBeHiddenAsync();
	}

	[Test]
	public async Task Test_WhenGivenAnMdblistList_LoadsTheListItemsIntoThePreview()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");

		await ConfigureFeedAsync(page, "E2E Preview Feed");
		await TestAsync(page);

		await Expect(page.GetByText("Mayday").First).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });
		await Expect(page.GetByText("The Runner").First).ToBeVisibleAsync();
	}

	private static async Task ConfigureFeedAsync(IPage page, string name)
	{
		await SelectMdblistAsync(page);
		await Fill(page, "#name", name);
		await Fill(page, "#list-identifier", ListIdentifier);
	}

	private static async Task Fill(IPage page, string selector, string value)
	{
		await page.Locator(selector).FillAsync(value);
		await page.Locator(selector).PressAsync("Tab");
	}

	private static async Task SelectMdblistAsync(IPage page)
	{
		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Mdblist", Exact = true }).First.ClickAsync();
		await page.WaitForTimeoutAsync(500);
	}

	private static async Task TestAsync(IPage page) =>
		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Test", Exact = true }).ClickAsync();
}
