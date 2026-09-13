namespace E2E.Tests.Auth;

public class AuthLifecycleTests
{
	[Test]
	public async Task SignOut_WhenAuthenticated_EndsTheSession()
	{
		var page = await E2EApp.AdminAt(Routes.Home);
		await page.ShouldNotBeAtAsync(Routes.SignIn);

		await page.VisitAsync("/auth/logout");
		await page.ShouldBeAtAsync(Routes.SignIn);

		await page.GotoAsync(Routes.Home);

		await page.ShouldBeAtAsync(Routes.SignIn);
	}
}
