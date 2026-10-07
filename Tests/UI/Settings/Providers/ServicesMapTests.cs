using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Domain.Providers;
using Domain.Settings;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.Providers;
using UI.Shared.Services.Branding;

namespace UI.Tests.Settings.Providers;

public class ServicesMapTests : BunitContext
{
	private static readonly ProviderConfig Config = new()
	{
		Sink = SinkKind.Plex,
		TrendingProvider = TrendingProvider.Tmdb,
		SearchProvider = SearchProvider.Plex,
		AuthProvider = AuthProvider.Plex,
		RegistrationMode = RegistrationMode.Open,
	};

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Services_ForTheViewersOwnSetup_SayWhetherEachIsReady()
	{
		var cut = RenderMap(canPick: false, RemoteService.Plex, RemoteService.Tmdb, RemoteService.AniList);

		var boxes = cut.FindAll(".map-service")
			.Select(service =>
				$"{service.QuerySelector(".map-service-name")!.TextContent.Trim()}: {service.QuerySelector(".map-service-status")!.TextContent.Trim()}"
			);
		var tiles = cut.FindAll(".map-tile").Select(tile => tile.QuerySelector(".sr-only")!.TextContent.Trim());
		await Assert.That(boxes).IsEquivalentTo(["Plex: Your account", "TMDb: Your key is set"]);
		await Assert.That(tiles).IsEquivalentTo(["MDBList: No key yet", "AniList: Ready", "MyAnimeList: No key yet", "Trakt: No key yet"]);
	}

	[Test]
	public async Task Traces_Always_StartFromTheRoleColumn()
	{
		var cut = RenderMap(canPick: false, RemoteService.Plex, RemoteService.Tmdb, RemoteService.Mdblist);

		var starts = cut.FindAll(".map-traces path").Select(path => path.GetAttribute("d")!.Split(' ')[0]);
		await Assert.That(starts).IsEquivalentTo(Enumerable.Repeat("M256", 9));
	}

	[Test]
	[Arguments(new[] { RemoteService.Plex, RemoteService.Tmdb, RemoteService.Mdblist, RemoteService.AniList, RemoteService.Mal, RemoteService.Trakt }, 0)]
	[Arguments(new[] { RemoteService.Plex, RemoteService.Tmdb, RemoteService.AniList }, 3)]
	[Arguments(new RemoteService[0], 9)]
	public async Task Traces_ToAServiceTheViewerHasNotSetUp_AreDotted(RemoteService[] ready, int dotted)
	{
		var cut = RenderMap(false, ready);

		await Assert.That(cut.FindAll(".map-traces path[stroke-dasharray]").Count).IsEqualTo(dotted);
	}

	[Test]
	public async Task Map_ForAnAdmin_OffersAChipForEachRoleWithASecondChoice()
	{
		var cut = RenderMap(canPick: true);

		await Assert
			.That(cut.FindAll(".map-chip").Select(chip => chip.GetAttribute("aria-label") ?? string.Empty))
			.IsEquivalentTo(["Change what Wantlist uses", "Change what Search uses"]);
	}

	[Test]
	public async Task Chip_WhenRadarrAndSonarrIsPicked_RaisesThatSink()
	{
		List<SinkKind> picked = [];
		var cut = Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<ServicesMap>(1);
			builder.AddComponentParameter(2, nameof(ServicesMap.Config), Config);
			builder.AddComponentParameter(3, nameof(ServicesMap.Ready), new HashSet<RemoteService>());
			builder.AddComponentParameter(4, nameof(ServicesMap.CanPick), true);
			builder.AddComponentParameter(5, nameof(ServicesMap.SinkPicked), EventCallback.Factory.Create<SinkKind>(this, picked.Add));
			builder.CloseComponent();
		});

		await cut.Find(".map-chip").ClickAsync();
		cut.WaitForElement("[role=menuitem]");
		await cut.FindAll("[role=menuitem]").Single(item => item.TextContent.Contains("Radarr")).ClickAsync();

		await Assert.That(picked).IsEquivalentTo([SinkKind.Arr]);
	}

	[Test]
	public async Task SearchChip_BeforeTheServicesAreConnected_OffersOnlyTheOnesThatAre()
	{
		var cut = RenderPicker(Config with { SearchProvider = SearchProvider.Tmdb }, new HashSet<RemoteService> { RemoteService.Tmdb }, _ => { });

		await cut.FindAll(".map-chip")[1].ClickAsync();
		cut.WaitForElement("[role=menuitem]");
		var items = cut.FindAll("[role=menuitem]").Select(item => $"{item.TextContent.Trim()} | {item.GetAttribute("aria-disabled") ?? "false"}");

		await Assert.That(items).IsEquivalentTo(["TMDb | false", "Radarr + SonarrConnect Radarr + Sonarr first | true", "Plex | false"]);
	}

	[Test]
	public async Task SearchChip_WhenAConnectedServiceIsPicked_RaisesIt()
	{
		List<SearchProvider> picked = [];
		var cut = RenderPicker(Config with { SearchProvider = SearchProvider.Tmdb }, new HashSet<RemoteService> { RemoteService.Tmdb }, picked.Add, arrReady: true);

		await cut.FindAll(".map-chip")[1].ClickAsync();
		cut.WaitForElement("[role=menuitem]");
		await cut.FindAll("[role=menuitem]").Single(item => item.TextContent.Contains("Radarr")).ClickAsync();

		await Assert.That(picked).IsEquivalentTo([SearchProvider.Arr]);
	}

	private IRenderedComponent<IComponent> RenderPicker(
		ProviderConfig config,
		IReadOnlySet<RemoteService> ready,
		Action<SearchProvider> picked,
		bool arrReady = false
	)
	{
		return Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<ServicesMap>(1);
			builder.AddComponentParameter(2, nameof(ServicesMap.Config), config);
			builder.AddComponentParameter(3, nameof(ServicesMap.Ready), ready);
			builder.AddComponentParameter(4, nameof(ServicesMap.ArrReady), arrReady);
			builder.AddComponentParameter(5, nameof(ServicesMap.CanPick), true);
			builder.AddComponentParameter(6, nameof(ServicesMap.SearchPicked), EventCallback.Factory.Create(this, picked));
			builder.CloseComponent();
		});
	}

	[Test]
	public async Task Map_ForAMember_OffersNoControl()
	{
		var cut = RenderMap(canPick: false);

		await Assert.That(cut.FindAll(".map-chip")).IsEmpty();
	}

	[Test]
	public async Task Map_ForAnAdmin_SaysNothingAboutWhoManagesIt()
	{
		var cut = RenderMap(canPick: true);

		await Assert.That(cut.Markup).DoesNotContain("Managed by an admin");
	}

	[Test]
	public async Task Map_ForAMember_SaysWhoManagesIt()
	{
		var cut = RenderMap(canPick: false);

		await Assert.That(cut.Markup).Contains("Managed by an admin");
	}

	[Test]
	[Arguments(AuthProvider.Plex)]
	[Arguments(AuthProvider.Local)]
	public async Task SignIn_WhicheverWayTheInstallSignsIn_HasNoChip(AuthProvider signIn)
	{
		var cut = RenderMap(Config with { AuthProvider = signIn }, canPick: true);

		await Assert.That(cut.FindAll("[aria-label='Change what Sign-in uses']")).IsEmpty();
		await Assert.That(cut.FindAll("[aria-label='Change what Wantlist uses']")).IsNotEmpty();
	}

	[Test]
	public async Task SignIn_OnASammlarrAccountsInstall_LandsOnTheLocalAccountsBoxWithItsPeople()
	{
		var cut = Render<ServicesMap>(parameters =>
			parameters
				.Add(map => map.Config, Config with { AuthProvider = AuthProvider.Local })
				.Add(map => map.Ready, new HashSet<RemoteService>())
				.Add(map => map.People, 3)
		);

		var boxes = cut.FindAll(".map-service")
			.Select(service =>
				$"{service.QuerySelector(".map-service-name")!.TextContent.Trim()}: {service.QuerySelector(".map-service-status")!.TextContent.Trim()}"
			);
		await Assert.That(boxes).Contains("Sammlarr: Local accounts · 3 people");
	}

	[Test]
	public async Task Service_WhenClicked_AsksToOpenItsRow()
	{
		List<RemoteService> opened = [];
		var cut = Render<ServicesMap>(parameters =>
			parameters
				.Add(map => map.Config, Config)
				.Add(map => map.Ready, new HashSet<RemoteService>())
				.Add(map => map.ServiceOpened, EventCallback.Factory.Create<RemoteService>(this, opened.Add))
		);

		await Service(cut, "TMDb").ClickAsync();
		await cut.Find("button.map-tile").ClickAsync();

		await Assert.That(opened).IsEquivalentTo([RemoteService.Tmdb, RemoteService.Mdblist], CollectionOrdering.Matching);
	}

	[Test]
	public async Task SammlarrAccounts_WhenClicked_AsksToOpenTheAccountsOverASolidTrace()
	{
		List<RemoteService> opened = [];
		var accounts = 0;
		var cut = Render<ServicesMap>(parameters =>
			parameters
				.Add(map => map.Config, Config with { AuthProvider = AuthProvider.Local })
				.Add(map => map.Ready, new HashSet<RemoteService>())
				.Add(map => map.People, 3)
				.Add(map => map.ServiceOpened, EventCallback.Factory.Create<RemoteService>(this, opened.Add))
				.Add(map => map.AccountsOpened, EventCallback.Factory.Create(this, () => accounts++))
		);

		await Service(cut, "Sammlarr").ClickAsync();

		await Assert.That(accounts).IsEqualTo(1);
		await Assert.That(opened).IsEmpty();
		await Assert.That(cut.FindAll(".map-traces path")[0].HasAttribute("stroke-dasharray")).IsFalse();
	}

	[Test]
	[Arguments(SinkKind.Arr, true, "button")]
	[Arguments(SinkKind.Arr, false, "div")]
	[Arguments(SinkKind.Plex, true, "div")]
	public async Task RadarrAndSonarr_OnlyForAnAdminWhoseWantlistSyncsThere_OpensTheWantlistSync(SinkKind sink, bool canPick, string element)
	{
		var cut = RenderMap(Config with { Sink = sink, SearchProvider = SearchProvider.Arr }, canPick);

		await Assert.That(Service(cut, "Radarr + Sonarr").LocalName).IsEqualTo(element);
	}

	[Test]
	public async Task RadarrAndSonarr_WhenClicked_DrawsBothMarksInTheMixedColourAndAsksToOpenTheWantlistSync()
	{
		List<RemoteService> opened = [];
		var arr = 0;
		var cut = Render<ServicesMap>(parameters =>
			parameters
				.Add(map => map.Config, Config with { Sink = SinkKind.Arr })
				.Add(map => map.Ready, new HashSet<RemoteService>())
				.Add(map => map.ArrReady, true)
				.Add(map => map.CanPick, true)
				.Add(map => map.ServiceOpened, EventCallback.Factory.Create<RemoteService>(this, opened.Add))
				.Add(map => map.ArrOpened, EventCallback.Factory.Create(this, () => arr++))
		);
		var box = Service(cut, "Radarr + Sonarr");

		await Assert.That(box.QuerySelector(".map-service-status")!.TextContent.Trim()).IsEqualTo("Connected");
		await Assert.That(box.InnerHtml).Contains("radarr-square").And.Contains("sonarr-square");
		await Assert.That(box.GetAttribute("style")).Contains($"--brand: {Services.GetRequiredService<MediaProviderBrandingService>().Color(new Arr())}");
		await box.ClickAsync();
		await Assert.That(arr).IsEqualTo(1);
		await Assert.That(opened).IsEmpty();
	}

	[Test]
	public async Task RadarrAndSonarr_WhenNotConnected_SaysNotSetUp()
	{
		var cut = RenderMap(Config with { Sink = SinkKind.Arr }, canPick: true);

		await Assert.That(Service(cut, "Radarr + Sonarr").QuerySelector(".map-service-status")!.TextContent.Trim()).IsEqualTo("Not set up");
	}

	[Test]
	public async Task Service_ThatOpens_NamesWhatTheClickDoes()
	{
		var cut = RenderMap(canPick: false);

		await Assert.That(Service(cut, "TMDb").QuerySelector(".sr-only")!.TextContent).IsEqualTo("Open ");
	}

	private static IElement Service(IRenderedComponent<ServicesMap> cut, string name)
	{
		return cut.FindAll(".map-service").Single(service => service.QuerySelector(".map-service-name")!.TextContent.Trim() == name);
	}

	private IRenderedComponent<ServicesMap> RenderMap(bool canPick, params RemoteService[] ready) => RenderMap(Config, canPick, ready);

	private IRenderedComponent<ServicesMap> RenderMap(ProviderConfig config, bool canPick, params RemoteService[] ready)
	{
		return Render<ServicesMap>(parameters =>
			parameters.Add(map => map.Config, config).Add(map => map.Ready, ready.ToHashSet()).Add(map => map.CanPick, canPick)
		);
	}
}
