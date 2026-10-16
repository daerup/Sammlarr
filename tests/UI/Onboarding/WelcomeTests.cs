using BlazorBlueprint.Components;
using Bunit;
using Contracts.Auth;
using Contracts.Setup;
using Domain.Auth;
using Domain.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Onboarding.Pages;

namespace UI.Tests.Onboarding;

public class WelcomeTests : BunitContext
{
	private readonly Mock<IApiKeyService> _keys = IApiKeyService.Mock();
	private readonly Mock<IOnboardingService> _onboarding = IOnboardingService.Mock();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(_keys.Object);
		Services.AddSingleton(_onboarding.Object);
		JSInterop.Mode = JSRuntimeMode.Loose;
		_onboarding.NeedsKeyPromptAsync(Any()).Returns(true);
	}

	[Test]
	public async Task TestAndContinue_BeforeAKeyIsTyped_IsDisabled()
	{
		var cut = Render<Welcome>();

		await Assert.That(TestAndContinue(cut).HasAttribute("disabled")).IsTrue();
	}

	[Test]
	public async Task TestAndContinue_WithAWorkingKey_StoresItAndLetsThemIn()
	{
		var key = WorkingKey();
		var cut = Render<Welcome>();
		await TypeAsync(cut, " their-key ");

		await TestAndContinue(cut).ClickAsync(new());

		_keys.StoreAsync(key, Any()).WasCalled(Times.Once);
		await Assert.That(Services.GetRequiredService<NavigationManager>().Uri).IsEqualTo("http://localhost/");
	}

	[Test]
	public async Task TestAndContinue_WithAKeyTmdbRefuses_SaysWhyAndStays()
	{
		_keys.TestAsync(ApiKeyProvider.Tmdb, "wrong", Any()).Returns(Error.From("TMDB did not accept this key."));
		var cut = Render<Welcome>();
		await TypeAsync(cut, "wrong");

		await TestAndContinue(cut).ClickAsync(new());

		await Assert.That(cut.Find("[role=alert]").TextContent).Contains("TMDB did not accept this key.");
		_keys.StoreAsync(Any<IApiKey>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task TestAndContinue_WithAWorkingKey_ReturnsToWhereTheyWereGoing()
	{
		WorkingKey();
		Services.GetRequiredService<NavigationManager>().NavigateTo("/welcome?returnUrl=%2Ffeeds");
		var cut = Render<Welcome>();
		await TypeAsync(cut, "their-key");

		await TestAndContinue(cut).ClickAsync(new());

		await Assert.That(Services.GetRequiredService<NavigationManager>().Uri).IsEqualTo("http://localhost/feeds");
	}

	[Test]
	public async Task Opening_WhenTheyAlreadyHaveAKey_GoesStraightOn()
	{
		_onboarding.NeedsKeyPromptAsync(Any()).Returns(false);

		Render<Welcome>();

		await Assert.That(Services.GetRequiredService<NavigationManager>().Uri).IsEqualTo("http://localhost/");
	}

	private TestApiKey WorkingKey()
	{
		var key = new TestApiKey(ApiKeyProvider.Tmdb, "their-key");
		_keys.TestAsync(ApiKeyProvider.Tmdb, "their-key", Any()).Returns(Success.Of<IApiKey>(key));
		return key;
	}

	private static Task TypeAsync(IRenderedComponent<Welcome> cut, string key)
	{
		return cut.InvokeAsync(() => cut.FindComponents<BbInput>().Single(input => input.Instance.Id == "welcome-key").Instance.ValueChanged.InvokeAsync(key));
	}

	private static AngleSharp.Dom.IElement TestAndContinue(IRenderedComponent<Welcome> cut)
	{
		return cut.FindAll("button").Single(button => button.TextContent.Contains("Test and continue"));
	}
}
