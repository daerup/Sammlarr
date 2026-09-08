using System.Text.RegularExpressions;

namespace E2E.Tests;

public class SignInPageTests
{
	[Test]
	public async Task An_unauthenticated_visitor_is_shown_the_sign_in_page()
	{
		var page = await AspireApp.OpenPageAsync();

		await page.GotoAsync("/");
		await page.WaitForURLAsync(new Regex("/signin"));

		await Assert.That(await page.TitleAsync()).Contains("Sign in");
	}
}
