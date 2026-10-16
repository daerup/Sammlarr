using Bunit;
using Bunit.TestDoubles;
using Contracts.Identity;
using Contracts.Setup;
using Domain.Identity;
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

	private readonly TestCurrentUser _user = new() { IsAuthenticated = false };

	[Before(Test)]
	public void Setup()
	{
		_onboarding.NeedsAdminAsync(Any()).Returns(true);
		Services.AddSingleton(_onboarding.Object);
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
