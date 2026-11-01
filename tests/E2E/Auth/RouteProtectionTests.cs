namespace E2E.Tests.Auth;

[Devices(Device.Desktop, Device.Phone)]
public class RouteProtectionTests(Device device) : E2ETests(device)
{
	[Test]
	[Arguments(Routes.Home)]
	[Arguments(Routes.Wantlist)]
	[Arguments(Routes.Blocklist)]
	[Arguments(Routes.Feeds)]
	[Arguments(Routes.Browse)]
	[Arguments(Routes.Settings)]
	public async Task Visit_WhenUnauthenticated_RedirectsToTheSignInPageTitledSignIn(string route)
	{
		var page = await E2EApp.OpenPageAsync();

		await page.GotoAsync(route);

		await page.ShouldBeAtAsync(Routes.SignIn);
		await Assert.That(await page.TitleAsync()).Contains("Sign in");
	}
}
