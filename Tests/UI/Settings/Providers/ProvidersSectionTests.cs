using BlazorBlueprint.Components;
using Bunit;
using Bunit.TestDoubles;
using Contracts.Auth;
using Contracts.Identity;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Identity;
using Domain.Providers;
using Domain.Settings;
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

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddScoped<Notifier>();
		Services.AddSingleton(RatingStub.Context());
		Services.AddSingleton(_config.Object);
		Services.AddSingleton(_switch.Object);
		Services.AddSingleton<ICurrentUser>(_user);
		var keys = IApiKeyService.Mock();
		keys.GetAsync(Any<ApiKeyProvider>(), Any()).Returns(Error.From("No API key is configured for this provider."));
		Services.AddSingleton(keys.Object);
		var credentials = ICredentialService.Mock();
		credentials.GetAsync(Any<AuthProvider>(), Any()).Returns(Error.From("Not connected."));
		Services.AddSingleton(credentials.Object);
		ComponentFactories.AddStub<AccountRow>();
		ComponentFactories.AddStub<ApiKeyRow>();
		ComponentFactories.AddStub<ServicesMap>();
		ComponentFactories.AddStub<ArrSinkRow>();
		JSInterop.Mode = JSRuntimeMode.Loose;
		_config.GetAsync(Any()).Returns(OnPlex);
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
		await Assert.That(Map(cut).Get(map => map.Ready)).DoesNotContain(MediaProvider.Radarr);
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
	public async Task Section_ForAMember_HidesWantlistSyncButDrawsRadarrAndSonarrReady()
	{
		_user.Role = UserRole.Member;
		_config.GetAsync(Any()).Returns(OnPlex with { Sink = SinkKind.Arr });

		var cut = RenderSection();

		await Assert.That(cut.FindComponents<Stub<ArrSinkRow>>()).IsEmpty();
		await Assert.That(Map(cut).Get(map => map.Ready)).Contains(MediaProvider.Sonarr);
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
