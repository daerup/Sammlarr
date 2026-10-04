using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

public class ServicesMapTests
{
	[Test]
	public async Task Map_ForAMember_ShowsWhoManagesItAndOffersNoPicker()
	{
		var page = await E2EApp.MemberAt(SettingsPage.Section("providers"));

		await Expect(SettingsPage.MapService(page, "Plex")).ToBeVisibleAsync();
		await Expect(page.Text("Managed by an admin")).ToBeVisibleAsync();
		await Expect(page.Locator("button.map-service")).ToHaveCountAsync(0);
		await Expect(page.Locator(".map-chip")).ToHaveCountAsync(0);
	}

	[Test]
	public async Task Map_ForAnAdmin_OffersAChipOnlyForTheWantlistWhichHasASecondTarget()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("providers"));

		await Expect(SettingsPage.MapService(page, "Plex")).ToBeVisibleAsync();
		await Expect(page.Text("Managed by an admin")).ToHaveCountAsync(0);
		await Expect(page.Locator("button.map-service")).ToHaveCountAsync(0);
		await Expect(page.Locator(".map-chip")).ToHaveCountAsync(1);
		await Expect(page.Locator(".map-chip[aria-label='Change what Wantlist uses']")).ToBeVisibleAsync();
	}
}
