namespace E2E.Tests.Auth;

public class RouteProtectionTests
{
	[Test]
	[Arguments(Routes.Wantlist)]
	[Arguments(Routes.Blocklist)]
	[Arguments(Routes.Feeds)]
	[Arguments(Routes.Browse)]
	[Arguments(Routes.Settings)]
	public async Task Visit_WhenUnauthenticated_RedirectsToSignIn(string route)
	{
		var page = await E2EApp.OpenPageAsync();

		await page.GotoAsync(route);

		await page.ShouldBeAtAsync(Routes.SignIn);
	}
}
