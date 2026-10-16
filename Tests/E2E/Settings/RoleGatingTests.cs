using Microsoft.Playwright;

namespace E2E.Tests.Settings;

public class RoleGatingTests
{
	[Test]
	public async Task DiagnosticsExport_WhenSignedInAsMember_SendsThemHome()
	{
		var page = await E2EApp.OpenMemberPageAsync();

		var response = await page.Context.APIRequest.GetAsync("/diagnostics/export", new APIRequestContextOptions { MaxRedirects = 0 });

		await Assert.That(response.Status).IsEqualTo(302);
		await Assert.That(new Uri(response.Headers["location"]).AbsolutePath).IsEqualTo("/");
	}
}
