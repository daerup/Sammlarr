using BlazorBlueprint.Components;
using Bunit;
using Domain.Providers;
using Domain.Settings;
using Microsoft.Extensions.DependencyInjection;
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
		Services.AddSingleton<MediaProviderBrandingService>();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Services_ForTheViewersOwnSetup_SayWhetherEachIsReady()
	{
		var cut = RenderMap(canPick: false, MediaProvider.Plex, MediaProvider.Tmdb);

		var boxes = cut.FindAll(".map-service")
			.Select(service =>
				$"{service.QuerySelector(".map-service-name")!.TextContent.Trim()}: {service.QuerySelector(".map-service-status")!.TextContent.Trim()}"
			);
		var sources = cut.FindAll(".map-source")
			.Select(source => $"{source.QuerySelector(".map-source-name")!.TextContent.Trim()}: {source.QuerySelector(".sr-only")!.TextContent.Trim()}");
		await Assert.That(boxes).IsEquivalentTo(["Plex: Your account", "TMDb: Your key is set"]);
		await Assert.That(sources).IsEquivalentTo(["MDBList: No key yet"]);
	}

	[Test]
	public async Task Feeds_Always_ConnectStraightToEachSource()
	{
		var cut = RenderMap(canPick: false, MediaProvider.Plex, MediaProvider.Tmdb, MediaProvider.Mdblist);

		var lines = cut.FindAll(".map-lines path").Select(path => path.GetAttribute("d")![..2]);
		await Assert.That(lines).IsEquivalentTo(Enumerable.Repeat("M0", 6));
	}

	[Test]
	[Arguments(new[] { MediaProvider.Plex, MediaProvider.Tmdb, MediaProvider.Mdblist }, 0)]
	[Arguments(new[] { MediaProvider.Plex, MediaProvider.Tmdb }, 1)]
	[Arguments(new MediaProvider[0], 6)]
	public async Task Lines_ToAServiceTheViewerHasNotSetUp_AreDotted(MediaProvider[] ready, int dotted)
	{
		var cut = RenderMap(false, ready);

		await Assert.That(cut.FindAll(".map-lines path[stroke-dasharray]").Count).IsEqualTo(dotted);
	}

	[Test]
	public async Task Map_ForAnAdmin_OpensTheWantlistPickerFromItsTarget()
	{
		var cut = RenderMap(canPick: true);

		var picker = cut.Find("button.map-service");
		await Assert.That(picker.TextContent).Contains("Plex");
		await Assert.That(cut.Markup).DoesNotContain("Managed by an admin");
	}

	[Test]
	public async Task Map_ForAMember_IsReadOnlyAndSaysWhoManagesIt()
	{
		var cut = RenderMap(canPick: false);

		await Assert.That(cut.FindAll("button.map-service")).IsEmpty();
		await Assert.That(cut.Markup).Contains("Managed by an admin");
	}

	private IRenderedComponent<ServicesMap> RenderMap(bool canPick, params MediaProvider[] ready) =>
		Render<ServicesMap>(parameters =>
			parameters.Add(map => map.Config, Config).Add(map => map.Ready, (IReadOnlySet<MediaProvider>)ready.ToHashSet()).Add(map => map.CanPickSink, canPick)
		);
}
