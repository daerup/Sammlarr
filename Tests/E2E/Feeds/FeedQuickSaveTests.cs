using System.Globalization;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Feeds;

[NotInParallel]
public class FeedQuickSaveTests
{
	private const string Identifier = "e2e-quicksave";

	private const int EarlySaveMinItems = 100;

	private const int PageDelayMs = 8000;

	[Test]
	public async Task QuickSave_WhileThePreviewIsStillLoading_PersistsWithoutLeavingTheEditor()
	{
		var name = $"E2E Quick {Guid.NewGuid().ToString("N")[..6]}";
		await using var first = await E2EApp.StubAsync(Page(0, EarlySaveMinItems + 20, hasMore: true, delayMs: 0));
		await using var second = await E2EApp.StubAsync(Page(1000, 5, hasMore: true, delayMs: PageDelayMs));
		await using var third = await E2EApp.StubAsync(Page(2000, 5, hasMore: true, delayMs: PageDelayMs));
		await using var last = await E2EApp.StubAsync(Page(3000, 0, hasMore: false, delayMs: PageDelayMs));

		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.SelectMdblistAsync(page);
		await FeedEditor.FillAsync(page, "#name", name);
		await FeedEditor.FillAsync(page, "#list-identifier", Identifier);
		await FeedEditor.TestAsync(page);

		var quickSave = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Quick save" });
		await Expect(quickSave).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 60000 });
		await Expect(FeedEditor.SaveButton(page)).ToBeHiddenAsync();

		await quickSave.ClickAsync();

		await Expect(page.GetByText("Feed saved.")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });
		await Assert.That(new Uri(page.Url).AbsolutePath).IsEqualTo("/feeds/create");

		var feeds = await E2EApp.OpenAuthenticatedPageAsync();
		await feeds.VisitAsync("/feeds");

		await Expect(feeds.GetByRole(AriaRole.Row, new PageGetByRoleOptions { Name = name }))
			.ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });
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
