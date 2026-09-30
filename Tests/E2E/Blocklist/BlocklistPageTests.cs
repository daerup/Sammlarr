using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Blocklist;

public class BlocklistPageTests
{
	[Test]
	public async Task Blocklist_WhenAuthenticatedAndEmpty_ShowsEmptyState()
	{
		var page = await E2EApp.AdminAt(Routes.Blocklist);

		await page.ShouldNotBeAtAsync(Routes.SignIn);
		await Expect(page.Text("Nothing blocklisted.")).ToBeVisibleAsync(Wait.Settle);
	}
}
