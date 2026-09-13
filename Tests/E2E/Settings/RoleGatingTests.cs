using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

public class RoleGatingTests
{
	[Test]
	[Arguments("Cache")]
	[Arguments("Diagnostics")]
	[Arguments("Users")]
	[Arguments("Testing")]
	public async Task AdminSections_WhenSignedInAsMember_AreHidden(string label)
	{
		var page = await E2EApp.MemberAt(Routes.Settings);
		await Expect(page.Heading("Settings")).ToBeVisibleAsync();
		await Expect(page.Button(label)).ToBeHiddenAsync();

		await SettingsPage.VisitSectionAsync(page, label.ToLowerInvariant());

		await Expect(page.Button("Provider Settings")).ToBeVisibleAsync();
		await Expect(page.Button(label)).ToBeHiddenAsync();
	}

	[Test]
	public async Task DiagnosticsExport_WhenSignedInAsMember_IsRefused()
	{
		var page = await E2EApp.OpenMemberPageAsync();

		var response = await page.Context.APIRequest.GetAsync("/diagnostics/export", new APIRequestContextOptions { MaxRedirects = 0 });

		await Assert.That(response.Status).IsEqualTo(302);
		await Assert.That(response.Headers["location"]).Contains("AccessDenied");
	}
}
