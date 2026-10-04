using Bunit;
using Bunit.TestDoubles;
using Contracts.Identity;
using Contracts.Setup;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Onboarding.Components;
using UI.Features.Onboarding.Pages;

namespace UI.Tests.Onboarding;

public class SetupPageTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		var onboarding = IOnboardingService.Mock();
		onboarding.NeedsAdminAsync(Any()).Returns(true);
		Services.AddSingleton(onboarding.Object);
		Services.AddSingleton<ICurrentUser>(new TestCurrentUser { IsAuthenticated = false });
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
}
