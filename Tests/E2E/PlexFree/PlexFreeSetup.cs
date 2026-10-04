using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.PlexFree;

internal static class PlexFreeSetup
{
	internal const string AdminName = "ada";

	internal const string MemberName = "grace";

	private const string AdminPassword = "e2e-admin-passphrase";

	private const string TmdbKey = "e2e-tmdb-key";

	internal static async Task CreateAdminAsync(IPage page, PlexFreeStack stack)
	{
		await page.VisitAsync(Routes.Setup);
		await page.Labelled("Setup code").EnterAsync(await stack.SetupCodeAsync());
		await page.Locator("#signin-local").ClickAsync();
		await page.Labelled("Username").EnterAsync(AdminName);
		await page.Labelled("Password").EnterAsync(AdminPassword);
		await page.Labelled("Confirm password").EnterAsync(AdminPassword);
		await page.Button("Create admin account").ClickAsync();
		await Expect(page.Heading("Where your wantlist syncs")).ToBeVisibleAsync(Wait.Settle);
		await page.WaitForCircuitAsync();
	}

	internal static async Task SwitchToRadarrAndSonarrAsync(IPage page, PlexFreeStack stack)
	{
		await Expect(page.Text("Connect Plex to continue")).ToBeVisibleAsync();
		await page.Locator("#sink-Arr").ClickAsync();
		await ArrSections.ConnectAsync(page, "Radarr", stack.Radarr.Url);
		await ArrSections.ConnectAsync(page, "Sonarr", stack.Sonarr.Url);
		await page.ButtonContaining("Switch the wantlist to Radarr + Sonarr").ClickAsync();
		await Expect(page.Button("Next")).ToBeEnabledAsync(Wait.Settle);
	}

	internal static async Task AddTmdbKeyAsync(IPage page)
	{
		await Expect(page.Text("Add a TMDB key to continue")).ToBeVisibleAsync();
		var row = page.Locator("#provider-row-tmdb");
		await row.Locator("input[placeholder='Enter API key']").EnterAsync(TmdbKey);
		await row.Button("Test").ClickAsync();
		await row.Button("Save").ClickAsync();
		await Expect(page.Button("Next")).ToBeEnabledAsync(Wait.Settle);
	}

	internal static async Task<string> InviteAsync(IPage page, string username)
	{
		await page.ButtonContaining("Invite member").ClickAsync();
		var dialog = page.Modal("Invite member");
		await dialog.Labelled("Username").EnterAsync(username);
		await dialog.Button("Create link").ClickAsync();
		var link = page.Locator("[data-link]");
		await Expect(link).ToBeVisibleAsync();
		var url = await link.InnerTextAsync();
		await page.Button("Done").ClickAsync();
		return url;
	}

	internal static async Task<string> ReachDoneAsync(IPage page, PlexFreeStack stack)
	{
		await CreateAdminAsync(page, stack);
		await SwitchToRadarrAndSonarrAsync(page, stack);
		await page.Button("Next").ClickAsync();
		await AddTmdbKeyAsync(page);
		await page.Button("Next").ClickAsync();
		var invite = await InviteAsync(page, MemberName);
		await page.Button("Next").ClickAsync();
		await Expect(page.Heading("Sammlarr is set up")).ToBeVisibleAsync();
		return invite;
	}

	internal static async Task<string> FinishAsync(IPage page, PlexFreeStack stack)
	{
		var invite = await ReachDoneAsync(page, stack);
		await page.Button("Finish setup").ClickAsync();
		await StaysHomeAsync(page);
		return invite;
	}

	internal static async Task StaysHomeAsync(IPage page)
	{
		await page.ShouldBeAtAsync(Routes.Home);
		await page.ReloadAsync();
		await page.ShouldBeAtAsync(Routes.Home);
	}
}
