using E2E.Tests.Feeds;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Common;

public class StackIsolationTests
{
	private const string SharedName = "E2E Contended Feed";

	[Test]
	[Repeat(3)]
	public async Task Feed_WhenEveryWorkerSavesTheSameName_IsOnlyEverVisibleOnce()
	{
		var page = await E2EApp.AdminAt(Routes.CreateFeed);
		await FeedEditor.ConfigureAsync(page, SharedName);
		await FeedEditor.LoadAsync(page);
		await FeedEditor.SaveAndCloseAsync(page);

		await Expect(page.FeedCard(SharedName)).ToHaveCountAsync(1, Wait.Settle);

		await DeleteAsync(page);

		await Expect(page.FeedCard(SharedName)).ToHaveCountAsync(0, Wait.Settle);
	}

	private static async Task DeleteAsync(IPage page)
	{
		await page.FeedCard(SharedName).Labelled("Delete").ClickAsync();
		await Expect(page.Heading($"Delete \"{SharedName}\"?")).ToBeVisibleAsync();
		await page.AlertDialog.Button("Delete").ClickAsync();
	}
}
