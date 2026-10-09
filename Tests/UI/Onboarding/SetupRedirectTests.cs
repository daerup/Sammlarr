using Bunit;
using Contracts.Identity;
using Contracts.Setup;
using Domain.Identity;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Onboarding.Components;

namespace UI.Tests.Onboarding;

public class SetupRedirectTests : BunitContext
{
	private readonly Mock<IOnboardingService> _onboarding = IOnboardingService.Mock();
	private readonly TestCurrentUser _user = new();

	[Before(Test)]
	public void Setup()
	{
		Services.AddSingleton(_onboarding.Object);
		Services.AddSingleton<ICurrentUser>(_user);
	}

	[Test]
	public async Task Admin_BeforeSetupIsFinished_IsSentToSetup()
	{
		_onboarding.IsCompleteAsync(Any()).Returns(false);

		Render<SetupRedirect>();

		await Assert.That(Services.GetRequiredService<NavigationManager>().Uri).IsEqualTo("http://localhost/setup");
	}

	[Test]
	public async Task Admin_AfterSetup_StaysWhereTheyAre()
	{
		_onboarding.IsCompleteAsync(Any()).Returns(true);

		Render<SetupRedirect>();

		await Assert.That(Services.GetRequiredService<NavigationManager>().Uri).IsEqualTo("http://localhost/");
	}

	[Test]
	public async Task Member_IsNeverSentToSetup()
	{
		_user.Role = UserRole.Member;

		Render<SetupRedirect>();

		await Assert.That(Services.GetRequiredService<NavigationManager>().Uri).IsEqualTo("http://localhost/");
		_onboarding.IsCompleteAsync(Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task Member_WithoutAKey_IsAskedForOne()
	{
		_user.Role = UserRole.Member;
		_onboarding.NeedsKeyPromptAsync(Any()).Returns(true);

		Render<SetupRedirect>();

		await Assert.That(Services.GetRequiredService<NavigationManager>().Uri).IsEqualTo("http://localhost/welcome");
	}

	[Test]
	public async Task Member_ArrivingThroughALink_IsBroughtBackToItAfterwards()
	{
		_user.Role = UserRole.Member;
		_onboarding.NeedsKeyPromptAsync(Any()).Returns(true);
		Services.GetRequiredService<NavigationManager>().NavigateTo("/feeds/runs");

		Render<SetupRedirect>();

		await Assert.That(Services.GetRequiredService<NavigationManager>().Uri).IsEqualTo("http://localhost/welcome?returnUrl=%2Ffeeds%2Fruns");
	}
}
