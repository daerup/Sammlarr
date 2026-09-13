namespace E2E.Tests.Auth;

public class SignInPageTests
{
	[Test]
	public async Task Visit_WhenUnauthenticated_ShowsSignInPage()
	{
		var page = await E2EApp.OpenPageAsync();

		await page.GotoAsync(Routes.Home);
		await page.ShouldBeAtAsync(Routes.SignIn);

		await Assert.That(await page.TitleAsync()).Contains("Sign in");
	}
}
