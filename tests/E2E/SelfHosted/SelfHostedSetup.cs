using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.SelfHosted;

internal static class SelfHostedSetup
{
	internal const string AdminName = "ada";

	internal const string MemberName = "grace";

	internal const string AdminPassword = "e2e-admin-passphrase";

	internal const string MemberPassword = "e2e-member-passphrase";

	private const string TmdbKey = "e2e-tmdb-key";

	internal static async Task CreateAdminAsync(IPage page, SelfHostedStack stack)
	{
		await page.VisitAsync(Routes.Setup);
		await PasteSetupCodeAsync(page, await stack.SetupCodeAsync());
		await page.Locator("#signin-local").ClickAsync();
		await page.Labelled("Username").EnterAsync(AdminName);
		await page.Labelled("Password").EnterAsync(AdminPassword);
		await page.Labelled("Confirm password").EnterAsync(AdminPassword);
		await page.Button("Create admin account").ClickAsync();
		await Expect(page.Heading("Where your wantlist syncs")).ToBeVisibleAsync(Wait.Settle);
		await page.WaitForCircuitAsync();
	}

	private static async Task PasteSetupCodeAsync(IPage page, string code)
	{
		await page.WaitForFunctionAsync(
			"code => { const data = new DataTransfer(); data.setData('text', code); return !document.querySelector(\"input[aria-label='Setup code']\").dispatchEvent(new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true })); }",
			code,
			new PageWaitForFunctionOptions { Timeout = Wait.Settle, PollingInterval = 250 }
		);
		await Expect(page.Locator("input[name=code]")).ToHaveValueAsync(code.Replace("-", ""));
	}

	private static async Task SwitchToRadarrAndSonarrAsync(IPage page, SelfHostedStack stack)
	{
		await Expect(page.Button("Next")).ToBeDisabledAsync();
		await page.Locator("#sink-Arr").ClickAsync();
		await ArrSections.ConnectAsync(page, "Radarr", stack.Radarr.Url);
		await ArrSections.ConnectAsync(page, "Sonarr", stack.Sonarr.Url);
		await Expect(page.Button("Next")).ToBeEnabledAsync(Wait.Settle);
	}

	private static async Task AddTmdbKeyAsync(IPage page)
	{
		await Expect(page.Button("Next")).ToBeDisabledAsync();
		var row = page.Locator("#provider-row-tmdb [data-slot=tmdb]");
		await row.Locator("input[placeholder='Enter API key']").EnterAsync(TmdbKey);
		await row.Button("Test").ClickAsync();
		await row.Button("Save").ClickAsync();
		await Expect(page.Button("Next")).ToBeEnabledAsync(Wait.Settle);
	}

	private static async Task<string> InviteAsync(IPage page, string username)
	{
		await page.ButtonContaining("Invite member").ClickAsync();
		var dialog = page.Modal("Invite member");
		await dialog.Labelled("Username").EnterAsync(username);
		await dialog.Button("Create link").ClickAsync();
		return await IssuedLinkAsync(page);
	}

	internal static async Task<string> ReachDoneAsync(IPage page, SelfHostedStack stack)
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

	internal static async Task<string> FinishAsync(IPage page, SelfHostedStack stack)
	{
		var invite = await ReachDoneAsync(page, stack);
		await page.Button("Done").ClickAsync();
		await StaysHomeAsync(page);
		return invite;
	}

	internal static async Task JoinAsync(IPage member, string invite)
	{
		await OpenInviteAsync(member, invite);
		await member.Locator("#welcome-key").EnterAsync(TmdbKey);
		await member.Button("Test and continue").ClickAsync();
		await StaysHomeAsync(member);
	}

	private static async Task OpenInviteAsync(IPage member, string invite)
	{
		await member.VisitAsync(invite);
		await member.Labelled("New password").EnterAsync(MemberPassword);
		await member.Labelled("Confirm password").EnterAsync(MemberPassword);
		await member.Button("Join").ClickAsync();
		await Expect(member.Heading("One more thing")).ToBeVisibleAsync(Wait.Settle);
		await member.WaitForCircuitAsync();
	}

	internal static async Task<string> IssuedLinkAsync(IPage page)
	{
		var link = page.Locator("[data-link]");
		await Expect(link).ToBeVisibleAsync();
		var url = await link.InnerTextAsync();
		await page.Button("Done").ClickAsync();
		return url;
	}

	internal static async Task StaysHomeAsync(IPage page)
	{
		await page.ShouldBeAtAsync(Routes.Home);
		await page.ReloadAsync();
		await page.ShouldBeAtAsync(Routes.Home);
		await page.WaitForCircuitAsync();
	}
}
