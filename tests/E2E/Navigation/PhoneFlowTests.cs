using System.Text.RegularExpressions;
using E2E.Tests.Browse;
using E2E.Tests.Feeds;
using E2E.Tests.Settings;
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
	public async Task Tile_Tapped_OpensTheDetailsSheetAndSwipesAway()
	{
		var page = await E2EApp.AdminAt(Routes.Browse);
		var tile = TrendingTiles.Resolved(page, TrendingTiles.Title);
		var sheet = page.Locator(".media-detail-sheet-content");
		await Expect(tile).ToBeVisibleAsync(Wait.Settle);

		await tile.TapAsync();

		await Expect(sheet.Locator(".hero-band img")).ToHaveCountAsync(1, Wait.Settle);
		await SitsOnTheBottomAsync(page, ".media-detail-sheet-content");
		await page.GoBackAsync();
		await Expect(sheet).ToHaveCountAsync(0);
		await page.ShouldBeAtAsync(Routes.Browse);

		await tile.TapAsync();
		await SwipeDownAsync(page, sheet.Locator("[data-sheet-handle]"));

		await Expect(sheet).ToHaveCountAsync(0);
		await page.WaitForFunctionAsync("() => history.state?.sheet === undefined");
	}

	[Test]
	public async Task Search_FromTheBar_TakesTheScreen()
	{
		var page = await E2EApp.AdminAt(Routes.Wantlist);
		var trigger = page.Button("Search for a movie or show");
		var search = page.Modal("Search");
		await Expect(trigger).ToBeVisibleAsync(Wait.Settle);

		await trigger.TapAsync();

		var field = page.Locator("#search-input");
		await Expect(field).ToBeFocusedAsync();
		await page.WaitForFunctionAsync(
			"() => { const box = document.querySelector('.search-modal-content').getBoundingClientRect(); return box.top === 0 && box.left === 0 && box.width === innerWidth && Math.abs(box.height - innerHeight) < 1; }"
		);
		await page.Keyboard.PressAsync("Enter");
		await Expect(field).Not.ToBeFocusedAsync();
		await search.Button("Cancel").TapAsync();
		await Expect(search).ToHaveCountAsync(0);
		await page.WaitForFunctionAsync("() => history.state?.sheet === undefined");

		await trigger.TapAsync();
		await Expect(search).ToBeVisibleAsync();
		await page.GoBackAsync();
		await Expect(search).ToHaveCountAsync(0);
		await page.ShouldBeAtAsync(Routes.Wantlist);
	}

	[Test]
	public async Task SettingsCards_OnAPhone_RunFromEdgeToEdge()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("about"));
		var cards = page.Locator("main .section-card");
		await Expect(cards.First).ToBeVisibleAsync(Wait.Settle);

		var boxes = await cards.EvaluateAllAsync<double[][]>(
			"cards => cards.map(card => { const box = card.getBoundingClientRect(); return [box.left, box.width - innerWidth]; })"
		);

		await Assert.That(boxes).IsNotEmpty();
		await Assert.That(boxes.All(box => Math.Abs(box[0]) < 1 && Math.Abs(box[1]) < 1)).IsTrue();
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

		await page.Toolbar.Button("Run").TapAsync();

		var dialog = page.GetByRole(AriaRole.Dialog);
		var cancel = dialog.Locator("[data-dialog-cancel]");
		var run = dialog.Locator("[data-dialog-commit]");
		await Expect(run).ToBeVisibleAsync(Wait.Settle);
		await SitsOnTheBottomAsync(page, "[role=dialog]");
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
	public async Task RunHistory_RunTapped_OpensInASheet()
	{
		var (page, card) = await FeedsWithOneAsync();
		var edit = await card.Locator("a.feed-card-link").GetAttributeAsync("href");
		var name = await card.GetAttributeAsync("aria-label");
		await card.Labelled("Run").TapAsync();
		var confirm = page.GetByRole(AriaRole.Dialog).Locator("[data-dialog-commit]");
		await Expect(confirm).ToBeEnabledAsync(Wait.Preview);
		await confirm.TapAsync();
		await Expect(card.GetByTestId("last-run-bar")).ToBeVisibleAsync(Wait.Preview);
		var runs = edit!.Replace("/feeds/edit/", "/feeds/", StringComparison.Ordinal) + "/runs";
		await page.VisitAsync(runs);
		var item = page.Locator(".runs-item").First;
		var sheet = page.Locator("[data-sheet]:has(.runs-sheet)");
		await Expect(page.Locator("header .phone-title")).ToHaveTextAsync(name!, Wait.Settle);
		await Expect(page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Back to Feeds" })).ToHaveAttributeAsync("href", "/feeds");
		await Expect(page.Toolbar.Button("Edit feed")).ToBeVisibleAsync();
		await Expect(item).ToBeVisibleAsync(Wait.Settle);
		await Assert.That(await page.OverflowingAsync()).IsEmpty();

		await item.TapAsync();

		await Expect(sheet).ToBeVisibleAsync();
		await page.ShouldBeAtAsync(runs);
		await page.GoBackAsync();
		await Expect(sheet).ToHaveCountAsync(0);
		await page.WaitForFunctionAsync("() => history.state?.sheet === undefined");
		await page.ShouldBeAtAsync(runs);
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
	public async Task Editor_PreviewSlot_ShowsThePreview()
	{
		var page = await E2EApp.AdminAt(Routes.CreateFeed);
		var name = page.Locator("#name");
		await FeedEditor.ConfigureAsync(page, TestData.Name("Preview"));
		await FeedEditor.LoadAsync(page);

		await page.Toolbar.Button("Preview").TapAsync();

		await Expect(FeedEditor.Rows(page).First).ToBeVisibleAsync(Wait.Preview);
		await Expect(name).ToBeHiddenAsync();
		await Assert.That(await page.OverflowingAsync()).IsEmpty();
		await page.Toolbar.Button("Settings").TapAsync();
		await Expect(name).ToBeVisibleAsync();
		await Expect(page.Toolbar.Button("Save")).ToBeEnabledAsync(Wait.Preview);
	}

	[Test]
	public async Task Editor_FiltersAndLimit_FitTheScreen()
	{
		var page = await E2EApp.AdminAt(Routes.CreateFeed);
		await FeedEditor.ConfigureAsync(page, TestData.Name("Fit"));

		await FeedEditor.OpenOptionalSectionAsync(page, "Filters, Sorting & Limits");
		await FeedEditor.Toggle(page, "Enable filters").TapAsync();
		await FeedEditor.AddConditionAsync(page, "Title", "Matrix");
		await FeedEditor.Toggle(page, "Enable limit").TapAsync();

		await Expect(FeedEditor.Toggle(page, "Enable limit")).ToHaveAttributeAsync("aria-checked", "true");
		await Assert.That(await page.OverflowingAsync()).IsEmpty();
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
		await SwipeDownAsync(page, sheet.Locator("[data-sheet-handle]"));

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

	[Test]
	public async Task Home_OnAPhone_OffersSyncInTheFooter()
	{
		var page = await E2EApp.AdminAt(Routes.Home);

		await Expect(page.Toolbar.ButtonContaining("Sync")).ToBeEnabledAsync(Wait.Settle);
		await Expect(page.GetByRole(AriaRole.Group, new PageGetByRoleOptions { Name = "Wantlist sync", Exact = true })).ToBeHiddenAsync();
	}

	[Test]
	public async Task Wantlist_OnAPhone_ShowsItsActionsInTheFooter()
	{
		string[] slots = ["Select", "Filter", "Sync"];
		var page = await WantlistWithAsync(TrendingTiles.Title);
		var labels = page.Toolbar.Locator(".toolbar-slot-label:visible");
		var search = page.GetByRole(AriaRole.Searchbox);

		await Expect(labels).ToHaveTextAsync(slots, Wait.Settle);
		await Expect(page.ButtonContaining("Bulk edit")).ToBeHiddenAsync();
		await Expect(page.GetByRole(AriaRole.Group, new PageGetByRoleOptions { Name = "Wantlist sync", Exact = true })).ToBeHiddenAsync();
		await search.FocusAsync();
		await Expect(page.Toolbar).ToBeHiddenAsync();
		await search.BlurAsync();
		await Expect(page.Toolbar).ToBeVisibleAsync();
		await page.Toolbar.Button("Select").TapAsync();
		await Expect(page.Locator("header .phone-select-bar [role=status]")).ToHaveTextAsync("0 selected");
	}

	[Test]
	public async Task Sync_Tapped_OpensTheChangeSheetWithItsTotals()
	{
		var page = await WantlistWithAsync(TrendingTiles.Title);
		var sync = page.Toolbar.ButtonContaining("Sync");
		var sheet = page.GetByRole(AriaRole.Dialog).Filter(new LocatorFilterOptions { HasText = "Review changes before syncing" });

		await Expect(sync).ToHaveAccessibleNameAsync(new Regex("^Sync, (?!comparing)"), new LocatorAssertionsToHaveAccessibleNameOptions { Timeout = Wait.Settle });
		await sync.TapAsync();

		await Expect(sheet.Locator("[data-testid=sync-total]")).ToHaveCountAsync(1, Wait.Settle);
		await SitsOnTheBottomAsync(page, "[role=dialog]");
		await page.GoBackAsync();
		await Expect(sheet).ToHaveCountAsync(0);
	}

	[Test]
	public async Task Filter_Applied_NarrowsTheList()
	{
		var page = await WantlistWithAsync(TrendingTiles.Title);
		var title = page.Heading(TrendingTiles.Title);
		var filter = page.Toolbar.ButtonContaining("Filter");
		var sheet = page.Modal("Filter and sort");
		var show = sheet.Locator("[data-dialog-commit]");
		await Expect(title).ToBeVisibleAsync(Wait.Settle);

		await filter.TapAsync();
		await sheet.Radio("Shows").TapAsync();
		await Expect(show).ToHaveTextAsync("Show 0 titles");
		await page.GoBackAsync();
		await Expect(sheet).ToHaveCountAsync(0);
		await Expect(title).ToBeVisibleAsync();

		await filter.TapAsync();
		await Expect(show).ToHaveTextAsync("Show 1 title");
		await SitsOnTheBottomAsync(page, "[role=dialog]");
		var clearBox = (await sheet.Button("Clear all").BoundingBoxAsync())!;
		var showBox = (await show.BoundingBoxAsync())!;
		await Assert.That(Math.Abs(showBox.Y - clearBox.Y)).IsLessThan(1);
		await Assert.That(showBox.Height).IsGreaterThanOrEqualTo(52);
		await Assert.That(clearBox.Width + showBox.Width).IsGreaterThanOrEqualTo(page.ViewportSize!.Width - 14);
		await sheet.Radio("Shows").TapAsync();
		await show.TapAsync();

		await Expect(sheet).ToHaveCountAsync(0);
		await Expect(title).ToBeHiddenAsync();
		await page.WaitForFunctionAsync("() => history.state?.sheet === undefined");
	}

	[Test]
	public async Task Browse_ScrolledToTheEnd_LoadsTheNextPage()
	{
		var page = await E2EApp.AdminAt(Routes.Browse);
		var tiles = page.Locator("[data-testid='media-tile']");
		await Expect(tiles.First).ToBeVisibleAsync(Wait.Settle);
		await Expect(page.Locator(".pager-pill")).ToBeHiddenAsync();
		await Expect(page.Heading("Second Page 1")).ToHaveCountAsync(0);

		await page.Locator(".pager-end").ScrollIntoViewIfNeededAsync();

		await Expect(tiles).ToHaveCountAsync(24, Wait.Settle);
		await Expect(page.Heading("Second Page 4")).ToHaveCountAsync(1);
	}

	[Test]
	public async Task SelectMode_TwoTilesPicked_ShowsTheCountAndTheBulkActions()
	{
		string[] bulk = ["Blocklist", "Remove"];
		string[] browsing = ["Select", "Filter", "Sync"];
		var page = await WantlistWithAsync(TrendingTiles.Title, TrendingTiles.Second);
		var layers = page.Locator("[data-testid='selection-layer']");
		var labels = page.Toolbar.Locator(".toolbar-slot-label:visible");

		await page.Toolbar.Button("Select").TapAsync();
		await layers.Nth(0).TapAsync();
		await layers.Nth(1).TapAsync();

		await Expect(page.Locator("header .phone-select-bar [role=status]")).ToHaveTextAsync("2 selected");
		await Expect(labels).ToHaveTextAsync(bulk);
		await Expect(page.Locator("header h1.phone-title")).ToBeHiddenAsync();
		await Expect(page.Button("Leave bulk edit")).ToBeHiddenAsync();
		await page.Button("Leave select mode").TapAsync();
		await Expect(labels).ToHaveTextAsync(browsing);
		await Expect(page.Locator("header .phone-select-bar")).ToHaveCountAsync(0);
	}

	private static async Task SitsOnTheBottomAsync(IPage page, string selector)
	{
		await page.WaitForFunctionAsync($"() => Math.abs(document.querySelector('{selector}').getBoundingClientRect().bottom - innerHeight) < 1");
	}

	private static async Task SwipeDownAsync(IPage page, ILocator handle)
	{
		await handle.HoverAsync();
		var box = (await handle.BoundingBoxAsync())!;
		await page.Mouse.DownAsync();
		await page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + 140, new MouseMoveOptions { Steps = 6 });
		await page.Mouse.UpAsync();
	}

	private static async Task<IPage> WantlistWithAsync(params string[] titles)
	{
		var page = await E2EApp.AdminAt(Routes.Browse);
		foreach (var title in titles)
		{
			var tile = TrendingTiles.Resolved(page, title);
			await Expect(tile).ToBeVisibleAsync(Wait.Settle);
			await tile.TapAsync();
			var add = page.ButtonContaining("Add to Wantlist");
			await add.TapAsync();
			await Expect(add).ToBeHiddenAsync(Wait.Settle);
			await page.Keyboard.PressAsync("Escape");
			await page.WaitForFunctionAsync("() => history.state?.sheet === undefined");
		}

		await page.VisitAsync(Routes.Wantlist);
		return page;
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
