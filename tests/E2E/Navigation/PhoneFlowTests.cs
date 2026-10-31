using E2E.Tests.Browse;
using E2E.Tests.Feeds;
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
	public async Task RunFeeds_OnAPhone_ShowsCancelAndRunAtTheBottom()
	{
		var (page, _) = await FeedsWithOneAsync();

		await page.Button("Run feeds").TapAsync();

		var dialog = page.GetByRole(AriaRole.Dialog);
		var cancel = dialog.Locator("[data-dialog-cancel]");
		var run = dialog.Locator("[data-dialog-commit]");
		await Expect(run).ToBeVisibleAsync(Wait.Settle);
		await page.WaitForFunctionAsync("() => Math.abs(document.querySelector('[role=dialog]').getBoundingClientRect().bottom - innerHeight) < 1");
		var viewport = page.ViewportSize!;
		var cancelBox = (await cancel.BoundingBoxAsync())!;
		var runBox = (await run.BoundingBoxAsync())!;
		await Assert.That(runBox.Y).IsEqualTo(cancelBox.Y);
		await Assert.That(runBox.Height).IsGreaterThanOrEqualTo(52);
		await Assert.That(runBox.Y + runBox.Height).IsGreaterThanOrEqualTo(viewport.Height - 7);
		await Assert.That(cancelBox.Width + runBox.Width).IsGreaterThanOrEqualTo(viewport.Width - 14);

		await page.GoBackAsync();

		await Expect(dialog).ToHaveCountAsync(0);
		await page.WaitForFunctionAsync("() => history.state?.sheet === undefined");
		await page.ShouldBeAtAsync(Routes.Feeds);
	}

	[Test]
	public async Task DeleteConfirm_OnBack_ClosesWithoutDeleting()
	{
		var (page, card) = await FeedsWithOneAsync();

		await card.Labelled("Delete").TapAsync();
		await Expect(page.AlertDialog).ToBeVisibleAsync();
		await page.GoBackAsync();

		await Expect(page.AlertDialog).ToHaveCountAsync(0);
		await page.WaitForFunctionAsync("() => history.state?.sheet === undefined");
		await page.ShouldBeAtAsync(Routes.Feeds);
		await page.VisitAsync(Routes.Feeds);
		await Expect(card).ToBeVisibleAsync(Wait.Settle);
	}

	[Test]
	public async Task Editor_OnBackWithChanges_LeavesOnlyOnDiscard()
	{
		var page = await E2EApp.AdminAt(Routes.Feeds);
		await page.Button("Add feed").TapAsync();
		await page.ShouldBeAtAsync(Routes.CreateFeed);
		await FeedEditor.ConfigureAsync(page, TestData.Name("Back"));

		await page.GoBackAsync();
		await Expect(page.GetByText("Leave without saving?")).ToBeVisibleAsync(Wait.Settle);
		await page.GoBackAsync();
		await Expect(page.AlertDialog).ToHaveCountAsync(0);
		await page.ShouldBeAtAsync(Routes.CreateFeed);

		await page.GoBackAsync();
		await page.Button("Keep editing").TapAsync();
		await Expect(page.AlertDialog).ToHaveCountAsync(0);
		await page.ShouldBeAtAsync(Routes.CreateFeed);

		await page.GoBackAsync();
		await page.Button("Discard changes").TapAsync();

		await page.ShouldBeAtAsync(Routes.Feeds);
		await page.WaitForFunctionAsync("() => history.state?.sheet === undefined");
	}

	[Test]
	public async Task HelpTip_Tapped_OpensItsSheetWithoutFocusingTheField()
	{
		var page = await E2EApp.AdminAt(Routes.CreateFeed);
		var help = page.Button("About Name");
		var sheet = page.Modal("Name");

		await help.TapAsync();

		await Expect(sheet).ToBeVisibleAsync();
		await sheet.GetByRole(AriaRole.Heading).TapAsync();
		await Expect(page.Locator("#name")).Not.ToBeFocusedAsync();
		await page.GoBackAsync();
		await Expect(sheet).ToHaveCountAsync(0);
		await page.ShouldBeAtAsync(Routes.CreateFeed);

		await help.TapAsync();
		var handle = sheet.Locator("[data-sheet-handle]");
		await handle.HoverAsync();
		var box = (await handle.BoundingBoxAsync())!;
		await page.Mouse.DownAsync();
		await page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + 140, new MouseMoveOptions { Steps = 6 });
		await page.Mouse.UpAsync();

		await Expect(sheet).ToHaveCountAsync(0);
		await page.WaitForFunctionAsync("() => history.state?.sheet === undefined");
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

	private static async Task<(IPage Page, ILocator Card)> FeedsWithOneAsync()
	{
		var name = TestData.Name("Phone");
		var page = await E2EApp.AdminAt(Routes.CreateFeed);
		await FeedEditor.ConfigureAsync(page, name);
		await FeedEditor.LoadAsync(page);
		await FeedEditor.SaveAndCloseAsync(page);
		var card = page.FeedCard(name);
		await Expect(card).ToBeVisibleAsync(Wait.Settle);
		return (page, card);
	}
}
