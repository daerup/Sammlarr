using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Home;

public class HomePageTests
{
	[Test]
	public async Task Visit_WhenAuthenticated_IsNotRedirectedToSignIn()
	{
		var page = await E2EApp.AdminAt(Routes.Home);

		await page.ShouldNotBeAtAsync(Routes.SignIn);
	}

	[Test]
	public async Task Home_WhenAuthenticated_ShowsLibraryHeading()
	{
		var page = await E2EApp.AdminAt(Routes.Home);

		await Expect(page.Heading("Your library")).ToBeVisibleAsync();
	}

	[Test]
	public async Task Shell_WhenAuthenticated_IdentifiesTheSignedInUser()
	{
		var page = await E2EApp.AdminAt(Routes.Home);

		await Expect(page.ButtonContaining("signed in as Test User")).ToBeVisibleAsync();
	}
}
