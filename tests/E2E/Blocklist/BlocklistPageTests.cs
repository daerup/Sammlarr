using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Blocklist;

public class BlocklistPageTests
{
	[Test]
	public async Task Blocklist_WhenEmpty_SaysSoAndBlocklistATitleOpensSearch()
	{
		var page = await E2EApp.MemberAt(Routes.Blocklist);

		await Expect(page.Text("Nothing blocklisted.")).ToBeVisibleAsync(Wait.Settle);

		await page.ButtonContaining("Blocklist a title").ClickAsync();

		await Expect(page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = "Blocklist a title" })).ToBeVisibleAsync();
	}
}
