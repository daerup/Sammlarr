using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Blacklist;

public class BlacklistPageTests
{
	[Test]
	public async Task Blacklist_WhenAuthenticatedAndEmpty_ShowsEmptyState()
	{
		var page = await E2EApp.AdminAt(Routes.Blacklist);

		await page.ShouldNotBeAtAsync(Routes.SignIn);
		await Expect(page.Text("Nothing blacklisted.")).ToBeVisibleAsync(Wait.Settle);
	}
}
