using BlazorBlueprint.Components;
using Bunit;
using Contracts.Identity;
using Domain.Identity;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Settings.Components.Providers.Accounts;

namespace UI.Tests.Settings.Providers.Accounts;

public class SammlarrAccountRowTests : BunitContext
{
	private readonly TestCurrentUser _user = new() { DisplayName = "guest" };

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		var users = IUserManagementService.Mock();
		users
			.GetPeopleAsync(Any())
			.Returns([Joined(UserRole.Admin, new None()), Joined(UserRole.Member, Some.Of(DateTimeOffset.UnixEpoch)), new InvitedPerson("lena", "lena")]);
		Services.AddSingleton(users.Object);
		Services.AddSingleton<ICurrentUser>(_user);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Row_ForTheSignedInPerson_WearsSammlarrsLogoAndSaysWhoIsSignedIn()
	{
		var cut = Render<SammlarrAccountRow>();

		await Assert.That(cut.FindAll(".provider-row svg.sammlarr-logo")).HasSingleItem();
		await Assert.That(cut.Find(".provider-row .sr-only").TextContent).IsEqualTo("Sammlarr");
		await Assert.That(cut.Markup).Contains("Signed in as guest");
	}

	[Test]
	public async Task Row_ForAnAdmin_CountsUsersAdminsPendingInvitesAndLockedOut()
	{
		var cut = Render<SammlarrAccountRow>(parameters => parameters.Add(row => row.Open, true));

		cut.WaitForState(() => cut.FindAll("[data-counts]").Count == 1);
		await Assert
			.That(cut.FindAll("[data-counts] > span").Select(count => count.TextContent.Words()))
			.IsEquivalentTo(["2 users", "1 admin", "1 invite pending", "1 locked out"]);
	}

	[Test]
	public async Task ManageUsers_ForAnAdmin_GoesToTheUsersSection()
	{
		var cut = Render<SammlarrAccountRow>(parameters => parameters.Add(row => row.Open, true));

		await cut.FindAll("button").Single(button => button.TextContent.Trim() == "Manage users").ClickAsync();

		await Assert.That(Services.GetRequiredService<NavigationManager>().Uri).EndsWith("/settings?section=users");
	}

	[Test]
	public async Task OpenYourAccount_ForAMember_GoesToTheirAccountWithoutCountingAnyone()
	{
		_user.Role = UserRole.Member;
		var cut = Render<SammlarrAccountRow>(parameters => parameters.Add(row => row.Open, true));

		await cut.FindAll("button").Single(button => button.TextContent.Trim() == "Open your account").ClickAsync();

		await Assert.That(Services.GetRequiredService<NavigationManager>().Uri).EndsWith("/settings?section=account");
		await Assert.That(cut.FindAll("[data-counts]")).IsEmpty();
	}

	private static JoinedPerson Joined(UserRole role, Optional<DateTimeOffset> lockedUntil)
	{
		var user = new User(UserId.New(), role.ToString(), string.Empty, role, DateTimeOffset.UnixEpoch);
		return new JoinedPerson(user, PersonHoldings.Empty, lockedUntil, new StoredColour(new None(), new None()));
	}
}
