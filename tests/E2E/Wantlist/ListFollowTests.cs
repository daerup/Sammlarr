using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Wantlist;

public class ListFollowTests
{
	private const string Resolved = "[data-testid='media-tile']:not(.tile-unmatched)";

	private const string Unwanted = $"{Resolved}:not(:has([fill='orange']))";

	[Test]
	public async Task Wantlist_WhenATitleIsAddedAndRemovedInAnotherTab_FollowsWithoutAReload()
	{
		var wantlist = await E2EApp.AdminAt(Routes.Wantlist);
		await Expect(wantlist.Heading("Wantlist")).ToBeVisibleAsync(Wait.Settle);
		var browse = await BrowseBesideAsync(wantlist);
		var title = await FirstTitleAsync(browse, Unwanted);
		await Expect(wantlist.Heading(title)).ToBeHiddenAsync();

		await OpenAsync(browse, title);
		await browse.Button("Add to wantlist").ClickAsync();

		await Expect(wantlist.Heading(title)).ToBeVisibleAsync(Wait.Settle);

		await browse.Button("Remove from wantlist").ClickAsync();

		await Expect(wantlist.Heading(title)).ToBeHiddenAsync(Wait.Settle);
	}

	[Test]
	public async Task Blocklist_WhenATitleIsBlocklistedInAnotherTab_FollowsWithoutAReload()
	{
		var blocklist = await E2EApp.AdminAt(Routes.Blocklist);
		await Expect(blocklist.Heading("Blocklist")).ToBeVisibleAsync(Wait.Settle);
		var browse = await BrowseBesideAsync(blocklist);
		var title = await FirstTitleAsync(browse, Resolved);
		await Expect(blocklist.Heading(title)).ToBeHiddenAsync();

		await OpenAsync(browse, title);
		await browse.Button("Add to blocklist").ClickAsync();

		await Expect(blocklist.Heading(title)).ToBeVisibleAsync(Wait.Settle);

		await browse.Button("Remove from blocklist").ClickAsync();

		await Expect(blocklist.Heading(title)).ToBeHiddenAsync(Wait.Settle);
	}

	private static async Task<IPage> BrowseBesideAsync(IPage page)
	{
		var browse = await page.Context.NewPageAsync();
		await browse.VisitAsync(Routes.Browse);
		return browse;
	}

	private static async Task<string> FirstTitleAsync(IPage browse, string tiles)
	{
		var tile = browse.Locator(tiles).First;
		await Expect(tile).ToBeVisibleAsync(Wait.Settle);
		return (await tile.Locator("h3").InnerTextAsync()).Trim();
	}

	private static Task OpenAsync(IPage browse, string title)
	{
		return browse.Locator(Resolved, new PageLocatorOptions { Has = browse.Heading(title) }).First.ClickAsync();
	}
}
