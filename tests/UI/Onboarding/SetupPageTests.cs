using Bunit;
using Bunit.TestDoubles;
using Contracts.Auth;
using Contracts.Identity;
using Contracts.Setup;
using Domain.Auth;
using Domain.Identity;
using Domain.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shared.Options;
using TestInfrastructure;
using UI.Features.Onboarding.Components;
using UI.Features.Onboarding.Pages;

namespace UI.Tests.Onboarding;

public class SetupPageTests : BunitContext
{
	private readonly Mock<IOnboardingService> _onboarding = IOnboardingService.Mock();

	private readonly Mock<ISignInService> _signIns = ISignInService.Mock();

	private readonly TestCurrentUser _user = new() { IsAuthenticated = false };

	[Before(Test)]
	public void Setup()
	{
		_onboarding.NeedsAdminAsync(Any()).Returns(true);
		_signIns.Pending(Any()).Returns(new None());
		Services.AddSingleton(_onboarding.Object);
		Services.AddSingleton(_signIns.Object);
		Services.AddSingleton(new AccountsOptions());
		Services.AddSingleton<ICurrentUser>(_user);
		ComponentFactories.AddStub<SetupWizard>();
	}

	[Test]
	[Arguments("local", "Local")]
	[Arguments("PLEX", "Plex")]
	[Arguments("nonsense", "nothing")]
	[Arguments("7", "nothing")]
	public async Task Page_BackFromAFailedSetup_PicksTheWayItWasToldIfThereIsOne(string with, string picked)
	{
		Services.GetRequiredService<NavigationManager>().NavigateTo($"/setup?with={with}");

		var cut = Render<Setup>();

		var wizard = cut.WaitForComponent<Stub<SetupWizard>>();
		await Assert.That(wizard.Instance.Parameters.Get(setup => setup.SignInWith)?.ToString() ?? "nothing").IsEqualTo(picked);
	}

	[Test]
	public async Task Page_BackFromPlexWithAPendingAdmin_HandsItToTheWizard()
	{
		var pending = new PendingAdmin(Guid.NewGuid(), "Daerup", string.Empty, HomeRole.Admin);
		_signIns.Pending(pending.Id).Returns(Some.Of(pending));
		Services.GetRequiredService<NavigationManager>().NavigateTo($"/setup?pending={pending.Id}");

		var cut = Render<Setup>();

		var wizard = cut.WaitForComponent<Stub<SetupWizard>>().Instance.Parameters;
		await Assert.That(wizard.Get(setup => setup.Pending)).IsSome().And.IsEqualTo(pending);
		await Assert.That(wizard.Get(setup => setup.Error)).IsNull();
	}

	[Test]
	public async Task Page_BackFromPlexAfterThePendingAdminExpired_SaysItTimedOutAndKeepsPlexPicked()
	{
		Services.GetRequiredService<NavigationManager>().NavigateTo($"/setup?pending={Guid.NewGuid()}");

		var cut = Render<Setup>();

		var wizard = cut.WaitForComponent<Stub<SetupWizard>>().Instance.Parameters;
		await Assert.That(wizard.Get(setup => setup.Pending)).IsNone();
		await Assert.That(wizard.Get(setup => setup.Error)).IsEqualTo("The Plex sign-in timed out. Please sign in again.");
		await Assert.That(wizard.Get(setup => setup.SignInWith)).IsEqualTo(AuthProvider.Plex);
	}

	[Test]
	public async Task Page_UseADifferentPlexAccountFromIsThisYou_DropsTheCardForTheSwitchSteps()
	{
		var pending = new PendingAdmin(Guid.NewGuid(), "Mia", string.Empty, HomeRole.Managed);
		_signIns.Pending(pending.Id).Returns(Some.Of(pending));
		var navigation = Services.GetRequiredService<NavigationManager>();
		navigation.NavigateTo($"/setup?pending={pending.Id}");
		var cut = Render<Setup>();
		cut.WaitForComponent<Stub<SetupWizard>>();

		await cut.InvokeAsync(() => navigation.NavigateTo("/setup?switch=plex"));

		var wizard = cut.FindComponent<Stub<SetupWizard>>().Instance.Parameters;
		await Assert.That(wizard.Get(setup => setup.Pending)).IsNone();
		await Assert.That(wizard.Get(setup => setup.SwitchingPlex)).IsTrue();
	}

	[Test]
	public async Task Page_ForAnAdminMidSetup_ShowsTheWizard()
	{
		AsAdmin(complete: false);

		var cut = Render<Setup>();

		await Assert.That(cut.WaitForComponent<Stub<SetupWizard>>().Instance.Parameters.Get(setup => setup.NeedsAdmin)).IsFalse();
	}

	[Test]
	public async Task Page_ForAnAdminWhoFinishedSetup_GoesHome()
	{
		AsAdmin(complete: true);

		var cut = Render<Setup>();

		await Assert.That(cut.FindComponents<Stub<SetupWizard>>()).IsEmpty();
		await Assert.That(Services.GetRequiredService<NavigationManager>().Uri).IsEqualTo("http://localhost/");
	}

	private void AsAdmin(bool complete)
	{
		_user.IsAuthenticated = true;
		_user.Role = UserRole.Admin;
		_onboarding.NeedsAdminAsync(Any()).Returns(false);
		_onboarding.IsCompleteAsync(Any()).Returns(complete);
		Services.GetRequiredService<NavigationManager>().NavigateTo("/setup");
	}
}
