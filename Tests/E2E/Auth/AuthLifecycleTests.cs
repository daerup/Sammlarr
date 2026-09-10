using System.Text.RegularExpressions;

namespace E2E.Tests.Auth;

public class AuthLifecycleTests
{
	[Test]
	public async Task SignOut_WhenAuthenticated_EndsTheSession()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();

		await page.VisitAsync("/");
		await Assert.That(page.Url).DoesNotContain("/signin");

		await page.VisitAsync("/auth/logout");
		await page.WaitForURLAsync(new Regex("/signin"));

		await page.VisitAsync("/");
		await page.WaitForURLAsync(new Regex("/signin"));

		await Assert.That(page.Url).Contains("/signin");
	}
}
