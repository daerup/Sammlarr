using AngleSharp.Dom;
using BlazorBlueprint.Components;
using Bunit;
using Bunit.TestDoubles;
using Contracts.Identity;
using Contracts.Settings;
using Contracts.Setup;
using Contracts.Wantlist;
using Domain.Providers;
using Domain.Settings;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shared.Options;
using TestInfrastructure;
using UI.Features.Onboarding.Components;
using UI.Features.Settings.Components.Providers;
using UI.Features.Settings.Components.Users;
using UI.Shared.Services;
using UI.Tests.Providers;

namespace UI.Tests.Onboarding;

public class SetupWizardTests : BunitContext
{
	private static readonly ProviderConfig OnArr = new()
	{
		Sink = SinkKind.Arr,
		TrendingProvider = TrendingProvider.Tmdb,
		SearchProvider = SearchProvider.Tmdb,
		AuthProvider = AuthProvider.Local,
		RegistrationMode = RegistrationMode.Open,
	};

	private readonly Mock<IOnboardingService> _onboarding = IOnboardingService.Mock();
	private readonly Mock<IProviderConfigService> _config = IProviderConfigService.Mock();
	private readonly HashSet<RemoteService> _connected = [];
	private readonly Mock<IUserManagementService> _users = IUserManagementService.Mock();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(new AccountsOptions());
		Services.AddScoped<Notifier>();
		Services.AddProviderBranding();
		Services.AddSingleton(_onboarding.Object);
		Services.AddSingleton(_config.Object);
		Services.AddSingleton(ReadinessStub.Connected(_connected));
		Services.AddSingleton(_users.Object);
		Services.AddSingleton(ISinkSwitch.Mock().Object);
		Services.AddSingleton<ICurrentUser>(new TestCurrentUser { DisplayName = "guest" });
		ComponentFactories.AddStub<AdminForm>();
		ComponentFactories.AddStub<ArrSinkRow>();
		ComponentFactories.AddStub<AccountRow>();
		ComponentFactories.AddStub<AccountsAndKeys>();
		ComponentFactories.AddStub<SignUpsCard>();
		ComponentFactories.AddStub<PeopleList>();
		JSInterop.Mode = JSRuntimeMode.Loose;
		_config.GetAsync(Any()).Returns(OnArr);
		_users.GetPeopleAsync(Any()).Returns([]);
	}

	[Test]
	public async Task Wizard_BackFromAWrongSetupCode_KeepsTheWayThatWasPicked()
	{
		var cut = Render<SetupWizard>(parameters => parameters.Add(wizard => wizard.NeedsAdmin, true).Add(wizard => wizard.SignInWith, AuthProvider.Local));

		await Assert.That(cut.FindComponent<Stub<AdminForm>>().Instance.Parameters.Get(form => form.Picked)).IsEqualTo(AuthProvider.Local);
	}

	[Test]
	public async Task Wizard_BeforeAnyoneExists_AsksForTheAdminAndOffersNoNext()
	{
		var cut = Render<SetupWizard>(parameters => parameters.Add(wizard => wizard.NeedsAdmin, true));

		await Assert.That(cut.FindComponents<Stub<AdminForm>>()).Count().IsEqualTo(1);
		await Assert.That(Buttons(cut, "Next")).IsEmpty();
	}

	[Test]
	public async Task Wizard_OnceTheAdminExists_StartsOnTheWantlistWithTheAccountDone()
	{
		var cut = RenderForAdmin();

		await Assert.That(cut.Find("h4").TextContent).IsEqualTo("Where your wantlist syncs");
		await Assert.That(cut.Find("nav button").InnerHtml).DoesNotContain(">1<");
	}

	[Test]
	public async Task Next_WhilePlexIsPickedButNotConnected_WaitsForPlex()
	{
		_config.GetAsync(Any()).Returns(OnArr with { Sink = SinkKind.Plex });

		var cut = RenderForAdmin();

		await Assert.That(Next(cut).HasAttribute("disabled")).IsTrue();
		await Assert.That(cut.Markup).Contains("Connect Plex to continue");
	}

	[Test]
	public async Task Next_WhileRadarrAndSonarrAreBeingSetUp_WaitsForTheSwitch()
	{
		_config.GetAsync(Any()).Returns(OnArr with { Sink = SinkKind.Plex });
		var cut = RenderForAdmin();

		await cut.InvokeAsync(() => cut.FindComponent<SinkCards>().Instance.PickedChanged.InvokeAsync(SinkKind.Arr));

		await Assert.That(cut.FindComponent<Stub<ArrSinkRow>>().Instance.Parameters.Get(row => row.SettingUp)).IsTrue();
		await Assert.That(cut.Markup).Contains("Connect Radarr and Sonarr to continue");
	}

	[Test]
	public async Task Wantlist_OnceItSyncsToRadarrAndSonarr_ShowsTheirRowAsSyncing()
	{
		var cut = RenderForAdmin();

		await Assert.That(cut.FindComponent<Stub<ArrSinkRow>>().Instance.Parameters.Get(row => row.Syncs)).IsTrue();
	}

	[Test]
	public async Task Next_OnTheKeysStep_WaitsForATmdbKey()
	{
		var cut = RenderForAdmin();

		await Next(cut).ClickAsync(new());

		await Assert.That(cut.Find("h4").TextContent).IsEqualTo("API keys");
		await Assert.That(Next(cut).HasAttribute("disabled")).IsTrue();
		await Assert.That(cut.Markup).Contains("Add a TMDB key to continue");
	}

	[Test]
	[Arguments(AuthProvider.Local, "Members")]
	[Arguments(AuthProvider.Plex, "Sign-ups")]
	public async Task FourthStep_FollowsHowPeopleSignIn(AuthProvider signIn, string heading)
	{
		_config.GetAsync(Any()).Returns(OnArr with { AuthProvider = signIn });
		WithTmdbKey();
		var cut = RenderForAdmin();

		await Next(cut).ClickAsync(new());
		await Next(cut).ClickAsync(new());

		await Assert.That(cut.Find("h4").TextContent).IsEqualTo(heading);
	}

	[Test]
	public async Task Skip_OnTheSignUpsStep_ClosesSignUpsAndShowsTheSummary()
	{
		_config.GetAsync(Any()).Returns(OnArr with { AuthProvider = AuthProvider.Plex });
		WithTmdbKey();
		var cut = RenderForAdmin();
		await Next(cut).ClickAsync(new());
		await Next(cut).ClickAsync(new());

		await Buttons(cut, "Skip").Single(button => button.TextContent.Trim() == "Skip").ClickAsync(new());

		_users.SetRegistrationModeAsync(RegistrationMode.Closed, Any()).WasCalled(Times.Once);
		await Assert.That(cut.Find("h4").TextContent).IsEqualTo("Sammlarr is set up");
	}

	[Test]
	public async Task FinishSetup_CompletesSetupAndGoesHome()
	{
		WithTmdbKey();
		var cut = RenderForAdmin();
		await Next(cut).ClickAsync(new());
		await Next(cut).ClickAsync(new());
		await Next(cut).ClickAsync(new());

		await Buttons(cut, "Finish setup").Single().ClickAsync(new());

		_onboarding.CompleteAsync(Any()).WasCalled(Times.Once);
		await Assert.That(Services.GetRequiredService<NavigationManager>().Uri).IsEqualTo("http://localhost/");
	}

	[Test]
	public async Task Wizard_FromTheWantlistToDone_OffersNoWayToSkipOrRunSetupAgain()
	{
		WithTmdbKey();
		var cut = RenderForAdmin();
		var steps = new List<string> { cut.Markup };

		foreach (var _ in Enumerable.Range(0, 3))
		{
			await Next(cut).ClickAsync(new());
			steps.Add(cut.Markup);
		}

		await Assert.That(cut.Find("h4").TextContent).IsEqualTo("Sammlarr is set up");
		await Assert.That(steps.Where(markup => markup.Contains("Skip setup", StringComparison.Ordinal))).IsEmpty();
		await Assert.That(cut.Markup).DoesNotContain("setup again");
	}

	private IRenderedComponent<SetupWizard> RenderForAdmin()
	{
		var cut = Render<SetupWizard>(parameters => parameters.Add(wizard => wizard.NeedsAdmin, false));
		cut.WaitForState(() => cut.FindAll(".choice-card").Count == 2);
		return cut;
	}

	private void WithTmdbKey() => _connected.Add(RemoteService.Tmdb);

	private static IElement Next(IRenderedComponent<SetupWizard> cut) => Buttons(cut, "Next").Single();

	private static IEnumerable<IElement> Buttons(IRenderedComponent<SetupWizard> cut, string text)
	{
		return cut.FindAll("button").Where(button => button.TextContent.Contains(text, StringComparison.Ordinal));
	}
}
