using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.SelfHosted;

[SelfHosted]
public class SetupTests
{
	[Test]
	public async Task AnyPage_OnAFreshInstall_GoesToSetup()
	{
		var page = await SelfHostedApp.OpenPageAsync();

		foreach (var route in new[] { Routes.Home, Routes.Wantlist, Routes.Settings })
		{
			await page.VisitAsync(route);
			await page.ShouldBeAtAsync(Routes.Setup);
		}

		await Expect(page.Heading("Create the admin account")).ToBeVisibleAsync();
	}

	[Test]
	public async Task Admin_CreatedWithTheLoggedSetupCode_IsASammlarrAccountSentBackToSetupUntilItIsFinished()
	{
		var stack = await SelfHostedApp.StackAsync();
		var page = await SelfHostedApp.OpenPageAsync();
		await SelfHostedSetup.CreateAdminAsync(page, stack);

		await page.VisitAsync(Routes.Home);
		await page.ShouldBeAtAsync(Routes.Setup);
		await Expect(page.Heading("Where your wantlist syncs")).ToBeVisibleAsync(Wait.Settle);
		await page.WaitForCircuitAsync();
		await page.Button("Back").ClickAsync();

		await Expect(page.Text($"{SelfHostedSetup.AdminName}, a Sammlarr account")).ToBeVisibleAsync();
	}

	[Test]
	public async Task Wizard_WithRadarrSonarrATmdbKeyAndAnInvite_SumsUpAndFinishesOnHome()
	{
		var stack = await SelfHostedApp.StackAsync();
		var page = await SelfHostedApp.OpenPageAsync();
		await SelfHostedSetup.ReachDoneAsync(page, stack);

		await Expect(page.Text("Syncs to Radarr + Sonarr")).ToBeVisibleAsync();
		await Expect(page.Text($"{SelfHostedSetup.MemberName}, invited")).ToBeVisibleAsync();
		await Expect(page.Text("Skip setup")).ToHaveCountAsync(0);
		await page.Button("Finish setup").ClickAsync();
		await SelfHostedSetup.StaysHomeAsync(page);
	}

	[Test]
	public async Task Invite_WhenTheMemberJoins_AsksForTheirTmdbKeyThenLetsThemIn()
	{
		var stack = await SelfHostedApp.StackAsync();
		var invite = await SelfHostedSetup.FinishAsync(await SelfHostedApp.OpenPageAsync(), stack);

		await SelfHostedSetup.JoinAsync(await SelfHostedApp.OpenPageAsync(), invite);
	}
}
