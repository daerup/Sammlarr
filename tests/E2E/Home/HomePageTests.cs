using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Home;

public class HomePageTests
{
	[Test]
	public async Task Home_WhenAuthenticated_ShowsLibraryHeading()
	{
		var page = await E2EApp.AdminAt(Routes.Home);

		await Expect(page.Heading("Your library")).ToBeVisibleAsync();
	}
}
