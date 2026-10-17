namespace E2E.Tests.Auth;

public class AuthLifecycleTests
{
	[Test]
	public async Task SignOut_FromTheAccountMenu_EndsTheSession()
	{
		var page = await E2EApp.AdminAt(Routes.Home);
		await page.ButtonContaining("signed in as Test User").ClickAsync();

		await page.ButtonContaining("Sign out").ClickAsync();

		await page.ShouldBeAtAsync(Routes.SignIn);
		await page.GotoAsync(Routes.Home);
		await page.ShouldBeAtAsync(Routes.SignIn);
	}
}
