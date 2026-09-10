using System.Text.RegularExpressions;

namespace E2E.Tests;

public class SignInPageTests
{
	[Test]
	public async Task Visit_WhenUnauthenticated_ShowsSignInPage()
	{
		var page = await E2EApp.OpenPageAsync();

		await page.VisitAsync("/");
		await page.WaitForURLAsync(new Regex("/signin"));

		await Assert.That(await page.TitleAsync()).Contains("Sign in");
	}
}
