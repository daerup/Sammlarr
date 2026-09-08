using Microsoft.Playwright;

namespace E2E.Tests;

public class BlacklistPageTests
{
	[Test]
	public async Task Blacklist_WhenAuthenticatedAndEmpty_ShowsEmptyState()
	{
		var page = await AspireApp.OpenAuthenticatedPageAsync();

		await page.GotoAsync("/blacklist");
		var emptyState = page.GetByText("Nothing blacklisted.");
		await emptyState.WaitForAsync();

		await Assert.That(page.Url).DoesNotContain("/signin");
		await Assert.That(await emptyState.IsVisibleAsync()).IsTrue();
	}
}
