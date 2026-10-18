using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Feeds;

public class FeedShuffleTests
{
	private const int Limit = 3;

	[Test]
	public async Task Save_WithARandomPick_RunsTheTitlesThePreviewShowed()
	{
		var name = TestData.Name("Shuffle");
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, name);
		await FeedEditor.OpenOptionalSectionAsync(page, "Filters, Sorting & Limits");
		await FeedEditor.Toggle(page, "Enable limit").ClickAsync();
		await FeedEditor.FillAsync(page, "#limit", TestData.Count(Limit));
		await FeedEditor.Option(page, "Maximum items", "At random").ClickAsync();
		await FeedEditor.LoadAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);
		var picked = await TitlesAsync(page, PreviewBadge.Included, PreviewBadge.Bypassed);

		await FeedEditor.SaveAndCloseAsync(page);
		await RunAsync(page, name);
		await page.FeedCard(name).Labelled("Edit").ClickAsync();
		await Expect(page.Locator("[data-preview-state='success']")).ToBeVisibleAsync(Wait.Preview);

		await Assert.That(await TitlesAsync(page, PreviewBadge.Kept, PreviewBadge.Bypassed)).IsEquivalentTo(picked);
	}

	private static async Task<IReadOnlyList<string>> TitlesAsync(IPage page, params PreviewBadge[] badges)
	{
		foreach (var badge in badges)
		{
			await FeedEditor.Badge(page, badge).ClickAsync();
		}

		await Expect(FeedEditor.Rows(page)).ToHaveCountAsync(Limit);
		return await FeedEditor.RowTitlesAsync(page);
	}

	private static async Task RunAsync(IPage page, string name)
	{
		var card = page.FeedCard(name);
		await Expect(card).ToBeVisibleAsync(Wait.Settle);
		await card.Labelled("Run").ClickAsync();
		var confirm = page.GetByRole(AriaRole.Dialog).Button("Run");
		await Expect(confirm).ToBeEnabledAsync(Wait.Preview);
		await confirm.ClickAsync();
		var live = page.GetByTestId("feed-run-live");
		await Expect(live).ToBeVisibleAsync(Wait.Settle);
		await Expect(live).ToBeHiddenAsync(Wait.Preview);
		await Expect(card.GetByTestId("last-run-bar")).ToBeVisibleAsync(Wait.Settle);
	}
}
