using Microsoft.Playwright;
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

	[Test]
	public async Task Blocklist_WhenEmpty_SaysWhatItDoesAndBlocklistATitleOpensSearch()
	{
		var page = await E2EApp.MemberAt(Routes.Blocklist);

		await Expect(page.Text("Nothing blocklisted.")).ToBeVisibleAsync(Wait.Settle);
		await Expect(page.GetByText("Feeds never add a blocklisted title.")).ToBeVisibleAsync();

		await page.ButtonContaining("Blocklist a title").ClickAsync();

		await Expect(page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = "Blocklist a title" })).ToBeVisibleAsync();
	}
}
