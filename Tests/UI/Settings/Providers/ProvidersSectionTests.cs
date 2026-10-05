using BlazorBlueprint.Components;
using Bunit;
using Bunit.TestDoubles;
using Contracts.Auth;
using Contracts.Identity;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Auth;
using Domain.Identity;
using Domain.Providers;
using Domain.Settings;
using Domain.Wantlist.Arr;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Settings.Components.Providers;
using UI.Shared.Services;
using UI.Tests.Media;

namespace UI.Tests.Settings.Providers;

public class ProvidersSectionTests : BunitContext
{
	private static readonly ProviderConfig OnPlex = new()
	{
		Sink = SinkKind.Plex,
		TrendingProvider = TrendingProvider.Tmdb,
		SearchProvider = SearchProvider.Plex,
		AuthProvider = AuthProvider.Plex,
		RegistrationMode = RegistrationMode.Open,
	};

	private readonly Mock<IProviderConfigService> _config = IProviderConfigService.Mock();

	private readonly Mock<ISinkSwitch> _switch = ISinkSwitch.Mock();

	private readonly TestCurrentUser _user = new();

	private readonly Mock<IArrSinkConfigService> _arrConfigs = IArrSinkConfigService.Mock();

	private readonly Mock<ICredentialService> _credentials = ICredentialService.Mock();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddScoped<Notifier>();
		Services.AddSingleton(RatingStub.Context());
		Services.AddSingleton(_config.Object);
		Services.AddSingleton(_switch.Object);
		Services.AddSingleton<ICurrentUser>(_user);
		Services.AddSingleton(IUserManagementService.Mock().Object);
		_arrConfigs.GetAsync(Any()).Returns(ArrSinkConfig.Empty);
		Services.AddSingleton(_arrConfigs.Object);
		var keys = IApiKeyService.Mock();
		keys.GetAsync(Any<ApiKeyProvider>(), Any()).Returns(Error.From("No API key is configured for this provider."));
		Services.AddSingleton(keys.Object);
		_credentials.GetAsync(Any<AuthProvider>(), Any()).Returns(Error.From("Not connected."));
		Services.AddSingleton(_credentials.Object);
		Services.AddScoped<ScrollService>();
		ComponentFactories.AddStub<AccountRow>();
		ComponentFactories.AddStub<SammlarrAccountRow>();
		ComponentFactories.AddStub<ApiKeyRow>();
		ComponentFactories.AddStub<ServicesMap>();
		ComponentFactories.AddStub<ArrSinkRow>();
		JSInterop.Mode = JSRuntimeMode.Loose;
		_config.GetAsync(Any()).Returns(OnPlex);
	}

	[Test]
	public async Task Section_OnASammlarrAccountsInstall_HeadsAccountsAndKeysWithTheSammlarrAccount()
	{
		_config.GetAsync(Any()).Returns(OnPlex with { AuthProvider = AuthProvider.Local });

		var cut = RenderSection();

		await Assert.That(cut.FindComponents<Stub<SammlarrAccountRow>>()).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Section_OnAPlexInstall_ShowsNoSammlarrAccount()
	{
		var cut = RenderSection();

		await Assert.That(cut.FindComponents<Stub<SammlarrAccountRow>>()).IsEmpty();
		await Assert.That(cut.FindComponents<Stub<AccountRow>>()).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Section_ForAnAdmin_OffersNoWayToRunSetupAgain()
	{
		var cut = RenderSection();

		await Assert.That(cut.Markup).DoesNotContain("Run setup again");
	}

	[Test]
	public async Task Section_OnPlex_ShowsNoWantlistSync()
	{
		var cut = RenderSection();

		await Assert.That(cut.FindComponents<Stub<ArrSinkRow>>()).IsEmpty();
	}

	[Test]
	public async Task PickingRadarrAndSonarr_OnPlex_OpensItsSetupAndDrawsItPending()
	{
		var cut = RenderSection();

		await cut.InvokeAsync(() => Map(cut).Get(map => map.SinkPicked).InvokeAsync(SinkKind.Arr));

		var row = cut.FindComponent<Stub<ArrSinkRow>>().Instance.Parameters;
		await Assert.That(row.Get(arr => arr.SettingUp)).IsTrue();
		await Assert.That(row.Get(arr => arr.Open)).IsTrue();
		await Assert.That(Map(cut).Get(map => map.Config).Sink).IsEqualTo(SinkKind.Arr);
		await Assert.That(Map(cut).Get(map => map.ArrReady)).IsFalse();
	}

	[Test]
	public async Task StayOnPlex_DuringSetup_PutsItAway()
	{
		var cut = RenderSection();
		await cut.InvokeAsync(() => Map(cut).Get(map => map.SinkPicked).InvokeAsync(SinkKind.Arr));

		await cut.InvokeAsync(() => cut.FindComponent<Stub<ArrSinkRow>>().Instance.Parameters.Get(arr => arr.StayOnPlex).InvokeAsync());

		await Assert.That(cut.FindComponents<Stub<ArrSinkRow>>()).IsEmpty();
		await Assert.That(Map(cut).Get(map => map.Config).Sink).IsEqualTo(SinkKind.Plex);
	}

	[Test]
	public async Task SwitchRequested_DuringSetup_SwitchesTheWantlist()
	{
		_switch.SwitchAsync(SinkKind.Arr, Any()).Returns(Success.Of(new SinkSwitched(12, 0)));
		var cut = RenderSection();
		await cut.InvokeAsync(() => Map(cut).Get(map => map.SinkPicked).InvokeAsync(SinkKind.Arr));
		_config.GetAsync(Any()).Returns(OnPlex with { Sink = SinkKind.Arr });

		await cut.InvokeAsync(() => cut.FindComponent<Stub<ArrSinkRow>>().Instance.Parameters.Get(arr => arr.SwitchRequested).InvokeAsync());

		_switch.SwitchAsync(SinkKind.Arr, Any()).WasCalled(Times.Once);
		await Assert.That(cut.FindComponent<Stub<ArrSinkRow>>().Instance.Parameters.Get(arr => arr.SettingUp)).IsFalse();
	}

	[Test]
	public async Task SwitchRequested_WhenRefused_KeepsTheSetupOpen()
	{
		_switch.SwitchAsync(SinkKind.Arr, Any()).Returns(Error.From("A wantlist sync is running right now. Switch again once it has finished."));
		var cut = RenderSection();
		await cut.InvokeAsync(() => Map(cut).Get(map => map.SinkPicked).InvokeAsync(SinkKind.Arr));

		await cut.InvokeAsync(() => cut.FindComponent<Stub<ArrSinkRow>>().Instance.Parameters.Get(arr => arr.SwitchRequested).InvokeAsync());

		await Assert.That(cut.FindComponent<Stub<ArrSinkRow>>().Instance.Parameters.Get(arr => arr.SettingUp)).IsTrue();
	}

	[Test]
	public async Task InstanceMoved_OnRadarrAndSonarr_MovesTheWantlistThroughTheSwitch()
	{
		_config.GetAsync(Any()).Returns(OnPlex with { Sink = SinkKind.Arr });
		_switch.MoveAsync(Any<ArrSinkConfig>(), Any()).Returns(Success.Of(new SinkSwitched(12, 0)));
		var cut = RenderSection();

		await cut.InvokeAsync(() => cut.FindComponent<Stub<ArrSinkRow>>().Instance.Parameters.Get(arr => arr.InstanceMoved).InvokeAsync(ArrSinkConfig.Empty));

		_switch.MoveAsync(ArrSinkConfig.Empty, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task PickingPlex_OnRadarrAndSonarr_SwitchesBack()
	{
		_config.GetAsync(Any()).Returns(OnPlex with { Sink = SinkKind.Arr });
		_switch.SwitchAsync(SinkKind.Plex, Any()).Returns(Success.Of(new SinkSwitched(12, 0)));
		var cut = RenderSection();

		await cut.InvokeAsync(() => Map(cut).Get(map => map.SinkPicked).InvokeAsync(SinkKind.Plex));

		_switch.SwitchAsync(SinkKind.Plex, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task SearchPicked_StoresTheSearchProvider()
	{
		var cut = RenderSection();

		await cut.InvokeAsync(() => Map(cut).Get(map => map.SearchPicked).InvokeAsync(SearchProvider.Tmdb));

		_config.StoreAsync(OnPlex with { SearchProvider = SearchProvider.Tmdb }, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task Section_OnPlexWithRadarrAndSonarrConnected_DrawsThemReadyForSearch()
	{
		var connected = new ArrInstanceConfig { Url = Some.Of("http://arr:7878"), ApiKey = Some.Of("key") };
		_arrConfigs.GetAsync(Any()).Returns(ArrSinkConfig.Empty with { Radarr = connected, Sonarr = connected });

		var cut = RenderSection();

		await Assert.That(Map(cut).Get(map => map.ArrReady)).IsTrue();
	}

	[Test]
	public async Task ArrRowChanged_OnceBothAppsAreConnected_DrawsThemReady()
	{
		var cut = RenderSection();
		await cut.InvokeAsync(() => Map(cut).Get(map => map.SinkPicked).InvokeAsync(SinkKind.Arr));
		var connected = new ArrInstanceConfig { Url = Some.Of("http://arr:7878"), ApiKey = Some.Of("key") };
		_arrConfigs.GetAsync(Any()).Returns(ArrSinkConfig.Empty with { Radarr = connected, Sonarr = connected });

		await cut.InvokeAsync(() => cut.FindComponent<Stub<ArrSinkRow>>().Instance.Parameters.Get(arr => arr.Changed).InvokeAsync());

		await Assert.That(Map(cut).Get(map => map.ArrReady)).IsTrue();
	}

	[Test]
	public async Task Section_ForAMember_HidesWantlistSyncButDrawsRadarrAndSonarrReady()
	{
		_user.Role = UserRole.Member;
		_config.GetAsync(Any()).Returns(OnPlex with { Sink = SinkKind.Arr });

		var cut = RenderSection();

		await Assert.That(cut.FindComponents<Stub<ArrSinkRow>>()).IsEmpty();
		await Assert.That(Map(cut).Get(map => map.ArrReady)).IsTrue();
	}

	[Test]
	public async Task ServiceOpened_ForTmdb_OpensItsRowAndScrollsToIt()
	{
		var cut = RenderSection();

		await cut.InvokeAsync(() => _ = Map(cut).Get(map => map.ServiceOpened).InvokeAsync(MediaProvider.Tmdb));

		var tmdb = cut.FindComponents<Stub<ApiKeyRow>>().Single(row => row.Instance.Parameters.Get(key => key.Provider) == ApiKeyProvider.Tmdb);
		await Assert.That(tmdb.Instance.Parameters.Get(key => key.Open)).IsTrue();
		await Assert.That(ScrolledTo(cut)).IsEqualTo("provider-row-tmdb");
	}

	[Test]
	public async Task ArrOpened_OpensTheWantlistSyncRingsItAndScrollsToIt()
	{
		_config.GetAsync(Any()).Returns(OnPlex with { Sink = SinkKind.Arr });
		var cut = RenderSection();

		await cut.InvokeAsync(() => Map(cut).Get(map => map.ArrOpened).InvokeAsync());

		await Assert.That(cut.FindComponent<CascadingValue<ProviderRowKey?>>().Instance.Value).IsEqualTo(new Arr());
		await Assert.That(cut.FindComponent<Stub<ArrSinkRow>>().Instance.Parameters.Get(arr => arr.Open)).IsTrue();
		await Assert.That(ScrolledTo(cut)).IsEqualTo("provider-row-arr");
	}

	[Test]
	public async Task AccountsOpened_AsAnAdmin_GoesToTheUsers()
	{
		var cut = RenderSection();

		await cut.InvokeAsync(() => Map(cut).Get(map => map.AccountsOpened).InvokeAsync());

		await Assert.That(Services.GetRequiredService<NavigationManager>().Uri).EndsWith("/settings?section=users");
	}

	[Test]
	public async Task AccountsOpened_AsAMember_OpensAndRingsTheSammlarrAccountRow()
	{
		_user.Role = UserRole.Member;
		_config.GetAsync(Any()).Returns(OnPlex with { AuthProvider = AuthProvider.Local });
		var cut = RenderSection();

		await cut.InvokeAsync(() => Map(cut).Get(map => map.AccountsOpened).InvokeAsync());

		await Assert.That(cut.FindComponent<CascadingValue<ProviderRowKey?>>().Instance.Value).IsEqualTo(new SammlarrAccounts());
		await Assert.That(cut.FindComponent<Stub<SammlarrAccountRow>>().Instance.Parameters.Get(row => row.Open)).IsTrue();
		await Assert.That(ScrolledTo(cut)).IsEqualTo("provider-row-sammlarr");
	}

	[Test]
	public async Task SearchPicked_PlexWithoutAPlexAccount_OpensThePlexRowAndStoresNothing()
	{
		_config.GetAsync(Any()).Returns(OnPlex with { SearchProvider = SearchProvider.Tmdb, Sink = SinkKind.Arr, AuthProvider = AuthProvider.Local });
		var cut = RenderSection();
		await Assert.That(cut.FindComponents<Stub<AccountRow>>()).IsEmpty();

		await cut.InvokeAsync(() => _ = Map(cut).Get(map => map.SearchPicked).InvokeAsync(SearchProvider.Plex));

		var plex = cut.FindComponent<Stub<AccountRow>>().Instance.Parameters;
		await Assert.That(plex.Get(row => row.Open)).IsTrue();
		await Assert.That(plex.Get(row => row.ReturnUrl)).IsEqualTo("/settings?section=providers&search=plex");
		_config.StoreAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task SearchAfterSignIn_WithPlexConnected_StoresPlexForSearch()
	{
		_config.GetAsync(Any()).Returns(OnPlex with { SearchProvider = SearchProvider.Tmdb });
		_credentials.GetAsync(AuthProvider.Plex, Any()).Returns(Success.Of(ICredential.Mock().Object));

		var cut = Render<ProvidersSection>(parameters => parameters.Add(section => section.SearchAfterSignIn, SearchProvider.Plex));

		cut.WaitForAssertion(() => _config.StoreAsync(OnPlex, Any()).WasCalled(Times.Once));
		await Assert.That(Services.GetRequiredService<NavigationManager>().Uri).EndsWith("/settings?section=providers");
	}

	[Test]
	public async Task SearchAfterSignIn_ForAMember_StoresNothing()
	{
		_user.Role = UserRole.Member;
		_config.GetAsync(Any()).Returns(OnPlex with { SearchProvider = SearchProvider.Tmdb });
		_credentials.GetAsync(AuthProvider.Plex, Any()).Returns(Success.Of(ICredential.Mock().Object));

		var cut = Render<ProvidersSection>(parameters => parameters.Add(section => section.SearchAfterSignIn, SearchProvider.Plex));
		cut.WaitForState(() => cut.FindComponents<Stub<ServicesMap>>().Count == 1);

		_config.StoreAsync(Any(), Any()).WasNeverCalled();
		await Assert.That(Map(cut).Get(map => map.Config).SearchProvider).IsEqualTo(SearchProvider.Tmdb);
	}

	[Test]
	public async Task PlexSignIn_AfterPickingPlexForSearch_StoresPlexForSearch()
	{
		_config.GetAsync(Any()).Returns(OnPlex with { SearchProvider = SearchProvider.Tmdb, Sink = SinkKind.Arr, AuthProvider = AuthProvider.Local });
		var cut = RenderSection();
		await cut.InvokeAsync(() => _ = Map(cut).Get(map => map.SearchPicked).InvokeAsync(SearchProvider.Plex));
		_credentials.GetAsync(AuthProvider.Plex, Any()).Returns(Success.Of(ICredential.Mock().Object));

		await cut.InvokeAsync(() => cut.FindComponent<Stub<AccountRow>>().Instance.Parameters.Get(row => row.Changed).InvokeAsync());

		_config.StoreAsync(OnPlex with { Sink = SinkKind.Arr, AuthProvider = AuthProvider.Local }, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task PlexRow_OnAPlexInstall_ReturnsToTheProviders()
	{
		var cut = RenderSection();

		await Assert.That(cut.FindComponent<Stub<AccountRow>>().Instance.Parameters.Get(row => row.ReturnUrl)).IsEqualTo("/settings?section=providers");
	}

	private string ScrolledTo(IRenderedComponent<ProvidersSection> cut)
	{
		cut.WaitForAssertion(() => JSInterop.VerifyInvoke("sammlarrScroll.intoView"));
		return (string)JSInterop.VerifyInvoke("sammlarrScroll.intoView").Arguments[0]!;
	}

	private static CapturedParameterView<ServicesMap> Map(IRenderedComponent<ProvidersSection> cut)
	{
		return cut.FindComponent<Stub<ServicesMap>>().Instance.Parameters;
	}

	private IRenderedComponent<ProvidersSection> RenderSection()
	{
		var cut = Render<ProvidersSection>();
		cut.WaitForState(() => cut.FindComponents<Stub<ServicesMap>>().Count == 1);
		return cut;
	}
}
