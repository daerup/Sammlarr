using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.PlexFree;

[PlexFree]
public class SetupTests
{
	private const string MemberPassword = "e2e-member-passphrase";

	[Test]
	public async Task AnyPage_OnAFreshInstall_GoesToSetup()
	{
		var page = await PlexFreeApp.OpenPageAsync();

		foreach (var route in new[] { Routes.Home, Routes.Wantlist, Routes.Settings })
		{
			await page.VisitAsync(route);
			await page.ShouldBeAtAsync(Routes.Setup);
		}

		await Expect(page.Heading("Create the admin account")).ToBeVisibleAsync();
	}

	[Test]
	public async Task Admin_WhenCreatedWithTheLoggedSetupCode_IsASammlarrAccount()
	{
		var stack = await PlexFreeApp.StackAsync();
		var page = await PlexFreeApp.OpenPageAsync();
		await PlexFreeSetup.CreateAdminAsync(page, stack);

		await page.Button("Back").ClickAsync();

		await Expect(page.Text($"{PlexFreeSetup.AdminName}, a Sammlarr account")).ToBeVisibleAsync();
	}

	[Test]
	public async Task Wizard_WithRadarrSonarrATmdbKeyAndAnInvite_SumsUpAndFinishesOnHome()
	{
		var stack = await PlexFreeApp.StackAsync();
		var page = await PlexFreeApp.OpenPageAsync();
		await PlexFreeSetup.ReachDoneAsync(page, stack);

		await Expect(page.Text("Syncs to Radarr + Sonarr")).ToBeVisibleAsync();
		await Expect(page.Text($"{PlexFreeSetup.MemberName}, invited")).ToBeVisibleAsync();
		await page.Button("Finish setup").ClickAsync();
		await PlexFreeSetup.StaysHomeAsync(page);
	}

	[Test]
	public async Task Invite_WhenTheMemberJoins_AsksForTheirTmdbKeyAndSkipForNowLetsThemIn()
	{
		var stack = await PlexFreeApp.StackAsync();
		var invite = await PlexFreeSetup.FinishAsync(await PlexFreeApp.OpenPageAsync(), stack);
		var member = await PlexFreeApp.OpenPageAsync();

		await member.VisitAsync(invite);
		await member.Labelled("New password").EnterAsync(MemberPassword);
		await member.Labelled("Confirm password").EnterAsync(MemberPassword);
		await member.Button("Join").ClickAsync();

		await Expect(member.Heading("One more thing")).ToBeVisibleAsync(Wait.Settle);
		await member.WaitForCircuitAsync();
		await member.Button("Skip for now").ClickAsync();
		await PlexFreeSetup.StaysHomeAsync(member);
	}
}
