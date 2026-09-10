using System.Text.RegularExpressions;

namespace E2E.Tests;

public class RouteProtectionTests
{
	[Test]
	[Arguments("/watchlist")]
	[Arguments("/blacklist")]
	[Arguments("/feeds")]
	[Arguments("/browse")]
	[Arguments("/settings")]
	public async Task Visit_WhenUnauthenticated_RedirectsToSignIn(string route)
	{
		var page = await E2EApp.OpenPageAsync();

		await page.GotoAsync(route);
		await page.WaitForURLAsync(new Regex("/signin"));

		await Assert.That(page.Url).Contains("/signin");
	}
}
