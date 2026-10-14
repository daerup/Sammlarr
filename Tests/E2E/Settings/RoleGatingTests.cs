using Microsoft.Playwright;

namespace E2E.Tests.Settings;

public class RoleGatingTests
{
	[Test]
	public async Task DiagnosticsExport_WhenSignedInAsMember_IsRefused()
	{
		var page = await E2EApp.OpenMemberPageAsync();

		var response = await page.Context.APIRequest.GetAsync("/diagnostics/export", new APIRequestContextOptions { MaxRedirects = 0 });

		await Assert.That(response.Status).IsEqualTo(302);
		await Assert.That(response.Headers["location"]).Contains("AccessDenied");
	}
}
