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
		var page = await E2EApp.OpenMemberPageAsync();

		await page.VisitAsync("/settings");
		await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Settings" })).ToBeVisibleAsync();
		await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = label, Exact = true })).ToBeHiddenAsync();

		await SettingsPage.VisitSectionAsync(page, label.ToLowerInvariant());

		await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Provider Settings", Exact = true })).ToBeVisibleAsync();
		await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = label, Exact = true })).ToBeHiddenAsync();
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
