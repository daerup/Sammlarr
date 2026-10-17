using E2E.Tests.Browse;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Wantlist;

public class ListFollowTests
{
	private const string Title = TrendingTiles.Title;

	[Test]
	public async Task Wantlist_WhenATitleIsAddedAndRemovedInAnotherTab_FollowsWithoutAReload()
	{
		var wantlist = await E2EApp.AdminAt(Routes.Wantlist);
		await Expect(wantlist.Heading("Wantlist")).ToBeVisibleAsync(Wait.Settle);
		var browse = await BrowseBesideAsync(wantlist);
		await Expect(wantlist.Heading(Title)).ToBeHiddenAsync();

		await TrendingTiles.Resolved(browse, Title).ClickAsync();
		await browse.Button("Add to wantlist").ClickAsync();

		await Expect(wantlist.Heading(Title)).ToBeVisibleAsync(Wait.Settle);

		await browse.Button("Remove from wantlist").ClickAsync();

		await Expect(wantlist.Heading(Title)).ToBeHiddenAsync(Wait.Settle);
	}

	[Test]
	public async Task Blocklist_WhenATitleIsBlocklistedInAnotherTab_FollowsWithoutAReload()
	{
		var blocklist = await E2EApp.AdminAt(Routes.Blocklist);
		await Expect(blocklist.Heading("Blocklist")).ToBeVisibleAsync(Wait.Settle);
		var browse = await BrowseBesideAsync(blocklist);
		await Expect(blocklist.Heading(Title)).ToBeHiddenAsync();

		await TrendingTiles.Resolved(browse, Title).ClickAsync();
		await browse.Button("Add to blocklist").ClickAsync();

		await Expect(blocklist.Heading(Title)).ToBeVisibleAsync(Wait.Settle);

		await browse.Button("Remove from blocklist").ClickAsync();

		await Expect(blocklist.Heading(Title)).ToBeHiddenAsync(Wait.Settle);
	}

	private static async Task<IPage> BrowseBesideAsync(IPage page)
	{
		var browse = await page.Context.NewPageAsync();
		await browse.VisitAsync(Routes.Browse);
		await Expect(TrendingTiles.Resolved(browse, Title)).ToBeVisibleAsync(Wait.Settle);
		return browse;
	}
}
