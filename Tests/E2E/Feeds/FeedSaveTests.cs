using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Feeds;

public class FeedSaveTests
{
	private const string Identifier = "e2e/slowsave";

	private const int PageDelayMs = 8000;

	[Test]
	public async Task Save_WhileThePreviewIsStillLoading_ForASwitchOff_PersistsAndLeaves()
	{
		var name = TestData.Name("Switch");
		await CreateAsync(name);
		await using var first = await E2EApp.StubAsync(Page(0, 5, hasMore: true, delayMs: 0));
		await using var slow = await E2EApp.StubAsync(Page(1000, 0, hasMore: false, delayMs: PageDelayMs));
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds");
		await page.FeedCard(name).Labelled("Edit").ClickAsync();
		await Expect(page.Locator("[data-preview-state='loading']")).ToBeVisibleAsync(Wait.Settle);

		await FeedEditor.Toggle(page, "Enabled").ClickAsync();
		await FeedEditor.SaveButton(page).ClickAsync();

		await Expect(page.FeedCard(name)).ToHaveClassAsync(new Regex("is-paused"), new LocatorAssertionsToHaveClassOptions { Timeout = Wait.Settle });
		await Assert.That(new Uri(page.Url).AbsolutePath).IsEqualTo("/feeds");
	}

	private static async Task CreateAsync(string name)
	{
		await using var whole = await E2EApp.StubAsync(Page(0, 5, hasMore: false, delayMs: 0));
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.SelectMdblistAsync(page);
		await FeedEditor.FillAsync(page, "#name", name);
		await FeedEditor.FillAsync(page, "#list-identifier", Identifier);
		await FeedEditor.TestAsync(page);
		await FeedEditor.SaveAsync(page);
		await Expect(page.FeedCard(name)).ToBeVisibleAsync(Wait.Settle);
	}

	private static StubSpec Page(int offset, int items, bool hasMore, int delayMs) =>
		new($"/lists/{Identifier}/items", 200, $"{{\"movies\":[{Items(items)}],\"shows\":[]}}")
		{
			Query = new Dictionary<string, string> { ["limit"] = "1000", ["offset"] = offset.ToString(CultureInfo.InvariantCulture) },
			Headers = hasMore ? new Dictionary<string, string> { ["X-Has-More"] = "true" } : new Dictionary<string, string>(),
			DelayMs = delayMs,
		};

	private static string Items(int count) =>
		string.Join(',', Enumerable.Range(0, count).Select(index => $"{{\"title\":\"E2E Unmatched {index}\",\"release_year\":2024}}"));
}
