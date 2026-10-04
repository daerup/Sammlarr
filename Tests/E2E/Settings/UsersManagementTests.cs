using System.Text.Json;
using Harness;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

public class UsersManagementTests
{
	private static readonly string MemberName = SammlarrTestHost.SeededMember.DisplayName;

	private static readonly string AdminName = SammlarrTestHost.SeededAdmin.DisplayName;

	private const int PinId = 515151;

	private const string AccessChanged = "Your access changed";

	[Test]
	public async Task RoleChange_WhenAppliedToMember_Persists()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("users"));
		await Expect(Badge(page, MemberName, "Member")).ToBeVisibleAsync();

		await ChangeRoleAsync(page, "Make admin");
		await Expect(Badge(page, MemberName, "Admin")).ToBeVisibleAsync();
		await SettingsPage.VisitSectionAsync(page, "users");
		await Expect(Badge(page, MemberName, "Admin")).ToBeVisibleAsync();

		var stale = await E2EApp.OpenMemberPageAsync();
		await stale.GotoAsync(Routes.Settings);
		await stale.ShouldBeAtAsync(Routes.SignIn);

		await ChangeRoleAsync(page, "Make member");
		await Expect(Badge(page, MemberName, "Member")).ToBeVisibleAsync();

		var restored = await E2EApp.MemberAt(Routes.Settings);

		await restored.ShouldNotBeAtAsync(Routes.SignIn);
	}

	[Test]
	public async Task RoleChange_WhileTheMemberHasTheAppOpen_ForcesThemToSignInAgain()
	{
		var member = await E2EApp.MemberAt(Routes.Feeds);
		await Expect(member.Heading("Feeds")).ToBeVisibleAsync();
		await Expect(member.Text(AccessChanged)).ToBeHiddenAsync();

		var admin = await E2EApp.AdminAt(SettingsPage.Section("users"));
		await ChangeRoleAsync(admin, "Make admin");

		await Expect(member.Text(AccessChanged)).ToBeVisibleAsync(Wait.Settle);

		await member.Keyboard.PressAsync("Escape");

		await Expect(member.Text(AccessChanged)).ToBeVisibleAsync();
		await member.ShouldBeAtAsync(Routes.Feeds);

		await member.GotoAsync(Routes.Browse);

		await member.ShouldBeAtAsync(Routes.SignIn);
		await Expect(member.Heading("Browse")).ToBeHiddenAsync();

		await ChangeRoleAsync(admin, "Make member");
		await Expect(Badge(admin, MemberName, "Member")).ToBeVisibleAsync();
	}

	[Test]
	public async Task RemoveAdmin_WhenLastAdmin_IsRefused()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("users"));

		await Expect(Badge(page, AdminName, "Admin")).ToBeVisibleAsync();
		await SettingsPage.OpenPersonAsync(page, AdminName);
		await Expect(SettingsPage.PersonRow(page, AdminName).Text("The last admin can't be made a member or removed.")).ToBeVisibleAsync();
		await Expect(SettingsPage.PersonRow(page, AdminName).Button("Remove")).ToBeHiddenAsync();
		await SettingsPage.OpenPersonAsync(page, MemberName);
		await Expect(SettingsPage.PersonRow(page, MemberName).Button("Remove")).ToBeVisibleAsync();
	}

	[Test]
	public async Task RemoveUser_WhenTheirNameIsTyped_DropsFromTheList()
	{
		var removable = SammlarrTestHost.SeededRemovable.DisplayName;
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

	private static async Task ChangeRoleAsync(IPage page, string action)
	{
		await SettingsPage.OpenPersonAsync(page, MemberName);
		await SettingsPage.PersonRow(page, MemberName).Button(action).ClickAsync();
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
