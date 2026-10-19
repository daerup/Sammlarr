using System.Text.Json;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

public class UsersManagementTests
{
	private const int PinId = 515151;

	private const string AccessChanged = "Your access changed";

	[Test]
	public async Task RoleChange_WhenAppliedToMember_Persists()
	{
		var member = (await E2EApp.MemberAsync()).DisplayName;
		var page = await E2EApp.AdminAt(SettingsPage.Section("users"));
		await Expect(Badge(page, member, "Member")).ToBeVisibleAsync();
		var stale = await E2EApp.OpenMemberPageAsync();

		await ChangeRoleAsync(page, member, "Make admin");
		await Expect(Badge(page, member, "Admin")).ToBeVisibleAsync();
		await SettingsPage.VisitSectionAsync(page, "users");
		await Expect(Badge(page, member, "Admin")).ToBeVisibleAsync();

		await stale.GotoAsync(Routes.Settings);
		await stale.ShouldBeAtAsync(Routes.SignIn);

		await ChangeRoleAsync(page, member, "Make member");
		await Expect(Badge(page, member, "Member")).ToBeVisibleAsync();
		await SettingsPage.VisitSectionAsync(page, "users");

		await Expect(Badge(page, member, "Member")).ToBeVisibleAsync();
	}

	[Test]
	public async Task RoleChange_WhileTheMemberHasTheAppOpen_ForcesThemToSignInAgain()
	{
		var name = (await E2EApp.MemberAsync()).DisplayName;
		var member = await E2EApp.MemberAt(Routes.Feeds);
		await Expect(member.Heading("Feeds")).ToBeVisibleAsync();
		await Expect(member.Text(AccessChanged)).ToBeHiddenAsync();

		var admin = await E2EApp.AdminAt(SettingsPage.Section("users"));
		await ChangeRoleAsync(admin, name, "Make admin");

		await Expect(member.Text(AccessChanged)).ToBeVisibleAsync(Wait.Settle);

		await member.Keyboard.PressAsync("Escape");

		await Expect(member.Text(AccessChanged)).ToBeVisibleAsync();
		await member.ShouldBeAtAsync(Routes.Feeds);

		await member.GotoAsync(Routes.Browse);

		await member.ShouldBeAtAsync(Routes.SignIn);
		await Expect(member.Heading("Browse")).ToBeHiddenAsync();
	}

	[Test]
	public async Task RemoveUser_WhenTheirNameIsTyped_DropsFromTheList()
	{
		var removable = (await E2EApp.MemberAsync()).DisplayName;
		var page = await E2EApp.AdminAt(SettingsPage.Section("users"));
		await Expect(SettingsPage.PersonRow(page, removable)).ToBeVisibleAsync();

		await SettingsPage.OpenPersonAsync(page, removable);
		await SettingsPage.PersonRow(page, removable).Button("Remove").ClickAsync();
		var dialog = page.Modal($"Remove {removable}?");
		await Expect(dialog.Button($"Remove {removable}")).ToBeDisabledAsync();
		await dialog.Locator("#remove-confirm").EnterAsync(removable);
		await dialog.Button($"Remove {removable}").ClickAsync();

		await Expect(SettingsPage.PersonRow(page, removable)).ToBeHiddenAsync(Wait.Settle);
		await SettingsPage.VisitSectionAsync(page, "users");
		await Expect(SettingsPage.PersonRow(page, removable)).ToBeHiddenAsync();
	}

	[Test]
	public async Task RegistrationClosed_WhenNewUserSignsIn_IsRejected()
	{
		await using var pins = await E2EApp.StubAsync("/api/v2/pins", 200, PinBody(), "POST");
		await using var pin = await E2EApp.StubAsync($"/api/v2/pins/{PinId}", 200, PinBody());
		await using var account = await E2EApp.StubAsync("/api/v2/user", 200, AccountBody());

		var admin = await E2EApp.AdminAt(SettingsPage.Section("users"));
		var registration = admin.GetByLabel("Allow new sign-ups");
		await Expect(registration).ToHaveAttributeAsync("aria-checked", "true");
		await registration.ClickAsync();
		await Expect(registration).ToHaveAttributeAsync("aria-checked", "false");

		var stranger = await E2EApp.OpenPageAsync();
		await stranger.VisitAsync(await stranger.PlexCallbackAsync());

		await stranger.ShouldBeAtAsync(Routes.SignIn);
		await Expect(stranger.GetByText("Registration is closed. Ask an admin for access.")).ToBeVisibleAsync();
		await Expect(SettingsPage.PersonRow(admin, "Stranger")).ToBeHiddenAsync();

		await registration.ClickAsync();
		await Expect(registration).ToHaveAttributeAsync("aria-checked", "true");
	}

	private static ILocator Badge(IPage page, string user, string role) => SettingsPage.PersonRow(page, user).Text(role);

	private static async Task ChangeRoleAsync(IPage page, string member, string action)
	{
		await SettingsPage.OpenPersonAsync(page, member);
		await SettingsPage.PersonRow(page, member).Button(action).ClickAsync();
		await page.AlertDialog.Button(action).ClickAsync();
	}

	private static string PinBody() =>
		JsonSerializer.Serialize(
			new
			{
				id = PinId,
				code = "e2e-code",
				product = "Sammlarr",
				trusted = true,
				qr = "e2e-qr",
				clientIdentifier = "e2e-client",
				expiresAt = "2030-01-01T00:00:00Z",
				authToken = "e2e-session-token",
				newRegistration = (string?)null,
			}
		);

	private static string AccountBody() =>
		JsonSerializer.Serialize(
			new
			{
				uuid = "e2e-stranger",
				username = "Stranger",
				title = "Stranger",
				thumb = string.Empty,
				email = "e2e-redacted",
				friendlyName = "Stranger",
			}
		);
}
