using BlazorBlueprint.Components;
using Bunit;
using Contracts.Auth;
using Domain.Auth;
using UI.Features.Onboarding.Components;

namespace UI.Tests.Onboarding;

public class PlexConfirmTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();

		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	[Arguments(HomeRole.Admin, "Plex Home admin")]
	[Arguments(HomeRole.Member, "Plex Home member")]
	public async Task Confirm_ForAFullAccount_NamesItsPlaceInThePlexHomeAndOffersBothWays(HomeRole home, string badge)
	{
		var pending = new PendingAdmin(Guid.NewGuid(), "Daerup", string.Empty, home);

		var cut = RenderConfirm(pending);

		await Assert.That(cut.Find("form input[name=pending]").GetAttribute("value")).IsEqualTo(pending.Id.ToString());
		await Assert.That(cut.Find("[data-pending-name]").TextContent).IsEqualTo("Daerup");
		await Assert.That(cut.Find("[data-home]").TextContent.Trim()).IsEqualTo(badge);
		await Assert.That(cut.Find("[data-confirm]").TextContent.Words()).IsEqualTo("Make Daerup the admin");
		await Assert.That(cut.Find("form").GetAttribute("action")).IsEqualTo("/auth/setup/confirm");
		await Assert.That(cut.Find("[data-switch]").GetAttribute("href")).IsEqualTo("/setup?switch=plex");
		await Assert.That(cut.FindAll("[data-managed]")).IsEmpty();
	}

	[Test]
	public async Task Confirm_ForAManagedHomeProfile_SaysItCannotBeTheAdminAndOnlyOffersAnotherAccount()
	{
		var cut = RenderConfirm(new PendingAdmin(Guid.NewGuid(), "Mia", string.Empty, HomeRole.Managed));

		await Assert
			.That(cut.Find("[data-managed]").TextContent.Words())
			.IsEqualTo("Mia is a managed Home profile, which can't be the admin. Sign in with your own Plex profile instead.");
		await Assert.That(cut.FindAll("[data-confirm]")).IsEmpty();
		await Assert.That(cut.Find("[data-switch]").TextContent.Trim()).IsEqualTo("Use a different Plex account");
	}

	[Test]
	public async Task Confirm_ForAnAccountOutsideAPlexHome_ShowsNoHomeBadge()
	{
		var cut = RenderConfirm(new PendingAdmin(Guid.NewGuid(), "Daerup", string.Empty, HomeRole.None));

		await Assert.That(cut.FindAll("[data-home]")).IsEmpty();
	}

	[Test]
	public async Task Confirm_InFirstSetup_SaysTheSetupCodeWasAccepted()
	{
		var cut = RenderConfirm(new PendingAdmin(Guid.NewGuid(), "Daerup", string.Empty, HomeRole.Admin));

		await Assert.That(cut.Find("[data-chosen]").TextContent.Words()).IsEqualTo("Setup code accepted · people sign in with Plex");
	}

	private IRenderedComponent<PlexConfirm> RenderConfirm(PendingAdmin pending) =>
		Render<PlexConfirm>(parameters => parameters.Add(confirm => confirm.Pending, pending));
}
