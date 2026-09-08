using System.Text.RegularExpressions;

namespace E2E.Tests;

public class SignInPageTests
{
	[Test]
	public async Task Visit_WhenUnauthenticated_ShowsSignInPage()
	{
		var page = await AspireApp.OpenPageAsync();

		await page.GotoAsync("/");
		await page.WaitForURLAsync(new Regex("/signin"));

		await Assert.That(await page.TitleAsync()).Contains("Sign in");
	}
}
