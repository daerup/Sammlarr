using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Wantlist;

public class WantlistPageTests
{
	[Test]
	public async Task Wantlist_WhenAuthenticated_ShowsTheWantlistHeading()
	{
		var page = await E2EApp.AdminAt(Routes.Wantlist);

		await page.ShouldNotBeAtAsync(Routes.SignIn);
		await Expect(page.Heading("Wantlist")).ToBeVisibleAsync();
	}

	[Test]
	public async Task Wantlist_WhenAuthenticated_OffersTheSearchShortcut()
	{
		var page = await E2EApp.AdminAt(Routes.Wantlist);

		await Expect(page.GetByText("Ctrl+K").First).ToBeVisibleAsync();
	}
}
