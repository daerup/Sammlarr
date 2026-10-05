using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts.Identity;
using Contracts.Settings;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Domain.Settings;
using Domain.Wantlist.Arr;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Shared.Media;

namespace UI.Tests.Media;

public class MediaTitleLinkTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		Services.AddMediaTitleLink();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Popover_ForEachLinkedId_KeepsTheProviderLogoOutsideTheLink()
	{
		var cut = Render<Harness>();

		await cut.Find(".media-title-trigger").ClickAsync(new());
		cut.WaitForElement(".id-links-popover-list");

		await Assert.That(cut.FindAll(".id-link-card .provider-tile")).Count().IsEqualTo(2);
		await Assert.That(cut.FindAll("a .provider-tile")).IsEmpty();
		await Assert
			.That(cut.FindAll("a.id-link").Select(link => link.GetAttribute("href") ?? string.Empty))
			.IsEquivalentTo(["https://www.themoviedb.org/movie/278", "https://www.imdb.com/title/tt0111161"], CollectionOrdering.Matching);
		await Assert.That(cut.FindAll("a.id-link")[1].GetAttribute("aria-label")).IsEqualTo("View tt0111161 on IMDb (opens in new tab)");
	}

	[Test]
	public async Task Popover_ForEachId_GivesItsRowTheProvidersBrandColourToHoverIn()
	{
		var cut = Render<Harness>();

		await cut.Find(".media-title-trigger").ClickAsync(new());
		cut.WaitForElement(".id-links-popover-list");

		await Assert
			.That(cut.FindAll(".id-link-card").Select(row => row.GetAttribute("style") ?? string.Empty))
			.IsEquivalentTo(["--brand: #01B4E4", "--brand: #F5DE50"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Popover_ForARottenTomatoesIdAndAnIdWithoutAPage_LinksTheFirstAndListsTheSecondPlain()
	{
		IReadOnlyList<IExternalMediaId> ids = [RottenTomatoesId.From("m/shawshank_redemption", MediaType.Movie), TvdbId.From("12", MediaType.Unknown)];
		var cut = Render<Harness>(parameters => parameters.Add(harness => harness.Ids, ids));

		await cut.Find(".media-title-trigger").ClickAsync(new());
		cut.WaitForElement(".id-links-popover-list");

		await Assert
			.That(cut.FindAll("a.id-link").Select(link => link.GetAttribute("href") ?? string.Empty))
			.IsEquivalentTo(["https://www.rottentomatoes.com/m/shawshank_redemption"]);
		await Assert.That(cut.FindAll(".id-link-readonly .id-link-value").Select(value => value.TextContent)).IsEquivalentTo(["12"]);
	}

	[Test]
	public async Task Popover_ForTwoIdsOfOneProvider_LinksEachToItsOwnPage()
	{
		IReadOnlyList<IExternalMediaId> ids = [PlexId.From("plex-a", MediaType.Movie), PlexId.From("plex-b", MediaType.Movie)];
		var cut = Render<Harness>(parameters => parameters.Add(harness => harness.Ids, ids));

		await cut.Find(".media-title-trigger").ClickAsync(new());
		cut.WaitForElement(".id-links-popover-list");

		const string discover = "https://app.plex.tv/desktop/#!/provider/tv.plex.provider.discover/details?key=%2Flibrary%2Fmetadata%2F";
		await Assert
			.That(cut.FindAll("a.id-link").Select(link => link.GetAttribute("href") ?? string.Empty))
			.IsEquivalentTo([$"{discover}plex-a", $"{discover}plex-b"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Popover_ForAnAdminWhoseWantlistSyncsToRadarrAndSonarr_AddsACardThatFindsTheTitleInRadarr()
	{
		Arrange(UserRole.Admin, SinkKind.Arr);
		var cut = Render<Harness>();

		await cut.Find(".media-title-trigger").ClickAsync(new());
		var card = cut.WaitForElement("a.id-link[href*='/add/new']");

		await Assert.That(card.GetAttribute("href")).IsEqualTo("http://radarr:7878/add/new?term=tmdb%3A278");
		await Assert.That(card.GetAttribute("aria-label")).IsEqualTo("Find tmdb:278 in Radarr (opens in new tab)");
		await Assert.That(card.QuerySelector(".id-link-value")!.TextContent).IsEqualTo("tmdb:278");
	}

	[Test]
	[Arguments(UserRole.Member, SinkKind.Arr)]
	[Arguments(UserRole.Admin, SinkKind.Plex)]
	public async Task Popover_ForAMemberOrOnAPlexWantlist_HasNoRadarrCard(UserRole role, SinkKind sink)
	{
		Arrange(role, sink);
		var cut = Render<Harness>();

		await cut.Find(".media-title-trigger").ClickAsync(new());
		cut.WaitForElement(".id-links-popover-list");

		await Assert.That(cut.FindAll("a.id-link[href*='/add/new']")).IsEmpty();
		await Assert.That(cut.FindAll(".id-link-card")).Count().IsEqualTo(2);
	}

	[Test]
	public async Task Render_BeforeThePopoverOpens_ReadsNoSettings()
	{
		var providers = Arrange(UserRole.Admin, SinkKind.Arr);

		var cut = Render<Harness>();

		await Assert.That(cut.FindAll(".media-title-trigger")).Count().IsEqualTo(1);
		providers.GetAsync(Any()).WasCalled(Times.Never);
	}

	private Mock<IProviderConfigService> Arrange(UserRole role, SinkKind sink)
	{
		var providers = IProviderConfigService.Mock();
		providers
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					Sink = sink,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Tmdb,
					AuthProvider = AuthProvider.Plex,
					RegistrationMode = RegistrationMode.Open,
				}
			);
		var arr = IArrSinkConfigService.Mock();
		arr.GetAsync(Any()).Returns(ArrSinkConfig.Empty with { Radarr = ArrInstanceConfig.Empty with { Url = Some.Of("http://radarr:7878") } });
		Services.AddSingleton<ICurrentUser>(new TestCurrentUser { Role = role });
		Services.AddSingleton(providers.Object);
		Services.AddSingleton(arr.Object);
		return providers;
	}

	private sealed class Harness : ComponentBase
	{
		[Parameter]
		public IReadOnlyList<IExternalMediaId> Ids { get; set; } = [TmdbId.From("278", MediaType.Movie), ImdbId.From("tt0111161", MediaType.Movie)];

		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<MediaTitleLink>(1);
			builder.AddComponentParameter(2, nameof(MediaTitleLink.Ids), Ids);
			builder.AddComponentParameter(3, nameof(MediaTitleLink.Title), "The Shawshank Redemption");
			builder.AddComponentParameter(4, nameof(MediaTitleLink.Side), PopoverSide.Bottom);
			builder.AddComponentParameter(5, nameof(MediaTitleLink.Align), PopoverAlign.Start);
			builder.CloseComponent();
		}
	}
}
