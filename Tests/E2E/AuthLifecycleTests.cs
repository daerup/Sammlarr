using System.Text.RegularExpressions;

namespace E2E.Tests;

public class AuthLifecycleTests
{
	[Test]
	public async Task SignOut_WhenAuthenticated_EndsTheSession()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();

		await E2EApp.GotoAsync(page, "/");
		await Assert.That(page.Url).DoesNotContain("/signin");

		await page.GotoAsync("/auth/logout");
		await page.WaitForURLAsync(new Regex("/signin"));

		await page.GotoAsync("/");
		await page.WaitForURLAsync(new Regex("/signin"));

		await Assert.That(page.Url).Contains("/signin");
	}
}
