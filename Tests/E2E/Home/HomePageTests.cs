using Microsoft.Playwright;

namespace E2E.Tests.Home;

public class HomePageTests
{
	[Test]
	public async Task Visit_WhenAuthenticated_IsNotRedirectedToSignIn()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();

		await page.VisitAsync("/");

		await Assert.That(page.Url).DoesNotContain("/signin");
	}

	[Test]
	public async Task Home_WhenAuthenticated_ShowsLibraryHeading()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();

		await page.VisitAsync("/");
		var heading = page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Your library" });
		await heading.WaitForAsync();

		await Assert.That(await heading.IsVisibleAsync()).IsTrue();
	}

	[Test]
	public async Task Shell_WhenAuthenticated_IdentifiesTheSignedInUser()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();

		await page.VisitAsync("/");
		var accountMenu = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "signed in as Test User" });
		await accountMenu.WaitForAsync();

		await Assert.That(await accountMenu.IsVisibleAsync()).IsTrue();
	}
}
