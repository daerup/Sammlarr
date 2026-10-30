using E2E.Tests.Browse;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Navigation;

[Devices(Device.Phone)]
public class PhoneFlowTests(Device device) : E2ETests(device)
{
	[Test]
	public async Task Menu_OpensAndNavigatesToASettingsSection()
	{
		var page = await E2EApp.AdminAt(Routes.Home);

		await page.Button("Open menu").ClickAsync();
		await page.Modal("Menu").GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Notifications", Exact = true }).ClickAsync();

		await page.ShouldBeAtAsync(Routes.Settings);
		await Expect(page.Modal("Menu")).ToBeHiddenAsync();
		await Expect(page.Locator("header .phone-title")).ToHaveTextAsync("Notifications");
	}

	[Test]
	public async Task BrowseTile_WhenTapped_KeepsItsSize()
	{
		var page = await E2EApp.AdminAt(Routes.Browse);
		var tile = TrendingTiles.Resolved(page, TrendingTiles.Title);
		await Expect(tile).ToBeVisibleAsync(Wait.Settle);

		await tile.TapAsync();

		await Expect(page.ButtonContaining("Add to Wantlist")).ToBeVisibleAsync(Wait.Settle);
		await Expect(tile).ToHaveCSSAsync("transform", "none");
	}

	[Test]
	public async Task Popover_TapAboveTheSheet_ClosesItWithoutTouchingThePage()
	{
		await using var wikidata = await E2EApp.StubAsync(Wikidata.Sparql("tt33764258", "m/the_odyssey_2026"));
		var page = await E2EApp.AdminAt(Routes.Browse);
		var badge = TrendingTiles.Resolved(page, "The Odyssey").Locator(".rating-badge-wrap > *");
		var sheet = page.Locator(".as-sheet");
		await Expect(badge).ToBeVisibleAsync(Wait.Settle);

		await badge.TapAsync();
		await Expect(sheet).ToBeVisibleAsync();
		await page.Touchscreen.TapAsync(30, 30);

		await Expect(sheet).ToHaveCountAsync(0);
		await page.WaitForFunctionAsync("() => history.state?.sheet === undefined");

		await badge.TapAsync();
		await Expect(sheet).ToBeVisibleAsync();
		await Expect(page.Modal("Menu")).ToHaveCountAsync(0);
		await page.GoBackAsync();

		await Expect(sheet).ToHaveCountAsync(0);
		await page.WaitForFunctionAsync("() => history.state?.sheet === undefined");
		await page.ShouldBeAtAsync(Routes.Browse);
		await Expect(badge).ToBeVisibleAsync();
	}

	[Test]
	public async Task Popover_OverTheTitleSheet_TakesTheTaps()
	{
		await using var wikidata = await E2EApp.StubAsync(Wikidata.Sparql("tt33764258", "m/the_odyssey_2026"));
		var page = await E2EApp.AdminAt(Routes.Browse);
		var tile = TrendingTiles.Resolved(page, "The Odyssey");
		await Expect(tile).ToBeVisibleAsync(Wait.Settle);
		await tile.TapAsync();
		var star = page.Locator(".media-detail-sheet-content .rating-badge");
		await Expect(star).ToBeVisibleAsync(Wait.Settle);

		await star.TapAsync();

		await page.Locator(".as-sheet .rating-row").First.TapAsync(new LocatorTapOptions { Trial = true });
		await Expect(page.Locator(".as-sheet a[href='https://www.rottentomatoes.com/m/the_odyssey_2026']").First).ToBeVisibleAsync(Wait.Settle);
	}

	[Test]
	public async Task OptionList_OpensAsASheetAcrossTheScreen()
	{
		var page = await E2EApp.AdminAt(Routes.CreateFeed);

		await page.Locator(".provider-trigger").TapAsync();

		var options = page.Locator(".as-sheet > :not(.sheet-handle)");
		await Expect(options).ToBeVisibleAsync(Wait.Settle);
		await Assert.That((await options.BoundingBoxAsync())!.Width).IsEqualTo(page.ViewportSize!.Width);
	}

	[Test]
	public async Task Menu_OnBack_ClosesWithoutLeavingThePage()
	{
		var page = await E2EApp.AdminAt(Routes.Browse);

		await page.Button("Open menu").ClickAsync();
		await Expect(page.Modal("Menu")).ToBeVisibleAsync();
		await page.GoBackAsync();

		await Expect(page.Modal("Menu")).ToHaveCountAsync(0);
		await page.ShouldBeAtAsync(Routes.Browse);
	}
}
