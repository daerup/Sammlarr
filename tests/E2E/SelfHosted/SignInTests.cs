using E2E.Tests.Settings;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.SelfHosted;

[SelfHosted]
public class SignInTests
{
	private const string WrongPassword = "not-the-passphrase";

	private const string NewPassword = "e2e-member-new-passphrase";

	private const string Refused = "Wrong username or password.";

	private const string LockedOut = "Too many failed sign-ins.";

	[Test]
	public async Task SignIn_WithAWrongPassword_IsRefusedUntilTheRightOneIsGiven()
	{
		var stack = await SelfHostedApp.StackAsync();
		await SelfHostedSetup.CreateAdminAsync(await SelfHostedApp.OpenPageAsync(), stack);
		var page = await SelfHostedApp.OpenPageAsync();

		await SignInAsync(page, SelfHostedSetup.AdminName, WrongPassword);

		await Expect(page.Text(Refused)).ToBeVisibleAsync();
		await StillSignedOutAsync(page);
		await SignInAsync(page, SelfHostedSetup.AdminName, SelfHostedSetup.AdminPassword);
		await page.ShouldBeAtAsync(Routes.Setup);
	}

	[Test]
	public async Task SignIn_AfterFiveWrongPasswords_RefusesEvenTheRightOne()
	{
		var stack = await SelfHostedApp.StackAsync();
		await SelfHostedSetup.CreateAdminAsync(await SelfHostedApp.OpenPageAsync(), stack);
		var page = await SelfHostedApp.OpenPageAsync();
		foreach (var _ in Enumerable.Range(0, 5))
		{
			await SignInAsync(page, SelfHostedSetup.AdminName, WrongPassword);
		}

		await SignInAsync(page, SelfHostedSetup.AdminName, SelfHostedSetup.AdminPassword);

		await Expect(page.GetByText(LockedOut)).ToBeVisibleAsync();
		await StillSignedOutAsync(page);
	}

	[Test]
	public async Task SignIn_WhileTheFormIsPosting_DisablesItsButtonAndSaysItIsSigningIn()
	{
		var stack = await SelfHostedApp.StackAsync();
		await SelfHostedSetup.CreateAdminAsync(await SelfHostedApp.OpenPageAsync(), stack);
		var page = await SelfHostedApp.OpenPageAsync();
		await page.VisitAsync(Routes.SignIn);
		await page.Labelled("Username").EnterAsync(SelfHostedSetup.AdminName);
		await page.Labelled("Password").EnterAsync(SelfHostedSetup.AdminPassword);
		await page.Button("Sign in").EvaluateAsync("button => button.form.addEventListener('submit', event => event.preventDefault())");

		await page.Button("Sign in").ClickAsync();

		await Expect(page.ButtonContaining("Signing in")).ToBeDisabledAsync();
	}

	[Test]
	public async Task ResetLink_FromTheAdmin_SetsANewPasswordAndRetiresTheOldOne()
	{
		var stack = await SelfHostedApp.StackAsync();
		var admin = await SelfHostedApp.OpenPageAsync();
		var invite = await SelfHostedSetup.FinishAsync(admin, stack);
		await SelfHostedSetup.JoinAsync(await SelfHostedApp.OpenPageAsync(), invite);
		await SettingsPage.VisitSectionAsync(admin, "users");
		await SettingsPage.OpenPersonAsync(admin, SelfHostedSetup.MemberName);
		await SettingsPage.PersonRow(admin, SelfHostedSetup.MemberName).ButtonContaining("Reset link").ClickAsync();
		var reset = await SelfHostedSetup.IssuedLinkAsync(admin);
		var member = await SelfHostedApp.OpenPageAsync();

		await member.VisitAsync(reset);
		await member.Labelled("New password").EnterAsync(NewPassword);
		await member.Labelled("Confirm password").EnterAsync(NewPassword);
		await member.Button("Save and sign in").ClickAsync();

		await SelfHostedSetup.StaysHomeAsync(member);
		var other = await SelfHostedApp.OpenPageAsync();
		await SignInAsync(other, SelfHostedSetup.MemberName, SelfHostedSetup.MemberPassword);
		await Expect(other.Text(Refused)).ToBeVisibleAsync();
		await SignInAsync(other, SelfHostedSetup.MemberName, NewPassword);
		await SelfHostedSetup.StaysHomeAsync(other);
	}

	[Test]
	public async Task SignOut_AfterSigningBackIn_EndsTheSessionAgain()
	{
		var stack = await SelfHostedApp.StackAsync();
		var page = await SelfHostedApp.OpenPageAsync();
		await SelfHostedSetup.FinishAsync(page, stack);
		await SignOutAsync(page);
		await SignInAsync(page, SelfHostedSetup.AdminName, SelfHostedSetup.AdminPassword);
		await SelfHostedSetup.StaysHomeAsync(page);

		await SignOutAsync(page);

		await StillSignedOutAsync(page);
	}

	[Test]
	public async Task ChangePassword_ForYourOwnAccount_SignsOutUntilTheNewOneIsUsed()
	{
		var stack = await SelfHostedApp.StackAsync();
		var page = await SelfHostedApp.OpenPageAsync();
		await SelfHostedSetup.FinishAsync(page, stack);
		await SettingsPage.VisitSectionAsync(page, "users");
		await page.Locator("[data-you]").ButtonContaining("Change password").ClickAsync();
		await page.Labelled("Current password").EnterAsync(SelfHostedSetup.AdminPassword);
		await page.Labelled("New password").EnterAsync(NewPassword);
		await page.Labelled("Confirm password").EnterAsync(NewPassword);

		await page.Locator("[data-you]").ButtonContaining("Change password").Last.ClickAsync();

		await page.ShouldBeAtAsync(Routes.SignIn);
		await StillSignedOutAsync(page);
		await SignInAsync(page, SelfHostedSetup.AdminName, NewPassword);
		await SelfHostedSetup.StaysHomeAsync(page);
	}

	private static async Task SignOutAsync(IPage page)
	{
		await page.ButtonContaining("Account menu").ClickAsync();
		await page.ButtonContaining("Sign out").ClickAsync();
		await page.ShouldBeAtAsync(Routes.SignIn);
		await page.WaitForCircuitAsync();
	}

	private static async Task SignInAsync(IPage page, string username, string password)
	{
		await page.VisitAsync(Routes.SignIn);
		await page.Labelled("Username").EnterAsync(username);
		await page.Labelled("Password").EnterAsync(password);
		await page.Button("Sign in").ClickAsync();
		await page.WaitForURLAsync(url => new Uri(url).PathAndQuery != Routes.SignIn);
		await page.WaitForCircuitAsync();
	}

	private static async Task StillSignedOutAsync(IPage page)
	{
		await page.VisitAsync(Routes.Home);
		await page.ShouldBeAtAsync(Routes.SignIn);
	}
}
