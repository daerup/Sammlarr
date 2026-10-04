using BlazorBlueprint.Components;
using Bunit;
using Contracts.Blocklist;
using Contracts.Media;
using Contracts.Media.Search;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Blocklist;
using Domain.Media;
using Domain.Providers;
using Domain.Settings;
using Domain.Wantlist;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Shared.Media;
using UI.Shared.Services;
using UI.Shared.Services.Branding;

namespace UI.Tests.Media;

public class GlobalSearchTests : BunitContext
{
	private const string Movie = "Inception";
	private const string Person = "Christopher Nolan";

	private static readonly MatchedMedia Matched = new(new MediaIdentity(MediaId.New(), [PlexId.From("1", MediaType.Movie)]), new MovieDetails { Title = Movie });

	private static readonly MediaMatchResult Unmatched = new UnmatchedMedia([PlexId.From("123", MediaType.Unknown)], new UnknownDetails { Title = Person });

	private readonly Mock<IWantlistService> _wantlist = IWantlistService.Mock();

	private readonly Mock<IProviderConfigService> _providerConfig = IProviderConfigService.Mock();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(RatingStub.Context());
		Services.AddLogging();
		Services.AddScoped<Notifier>();
		Services.AddSingleton<MediaProviderBrandingService>();
		Services.AddSingleton(ArtworkStub.Sizing());
		Services.AddSingleton(IMediaService.Mock().Object);

		_providerConfig
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					Sink = SinkKind.Plex,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Plex,
					AuthProvider = AuthProvider.Plex,
					RegistrationMode = RegistrationMode.Open,
				}
			);
		Services.AddSingleton(_providerConfig.Object);

		_wantlist.GetAsync(Any()).Returns(new WantlistSnapshot([]));
		Services.AddSingleton(_wantlist.Object);

		var blocklist = IBlocklistService.Mock();
		blocklist.GetAsync(Any()).Returns(new BlocklistSnapshot([]));
		Services.AddSingleton(blocklist.Object);

		var search = IMediaSearchService.Mock();
		search.SearchAsync(Any(), Any(), Any()).Returns([Matched, Unmatched]);
		Services.AddSingleton(search.Object);

		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Search_WithAMatchedAndAnUnmatchedResult_RendersBothButTogglesOnlyTheMatched()
	{
		var cut = Render<GlobalSearch>(parameters => parameters.Add(search => search.IsOpen, true));
		await cut.Find("#search-input").InputAsync(new ChangeEventArgs { Value = Person });

		await cut.Find("#search-input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

		var rows = cut.FindAll(".search-result-row");
		await Assert.That(rows).Count().IsEqualTo(2);
		await Assert.That(rows.Single(row => row.TextContent.Contains(Movie)).QuerySelectorAll(".search-result-toggle-btn")).IsNotEmpty();
		await Assert.That(rows.Single(row => row.TextContent.Contains(Person)).QuerySelectorAll(".search-result-toggle-btn")).IsEmpty();
	}

	[Test]
	[Arguments(SearchProvider.Tmdb, "Results from TMDB")]
	[Arguments(SearchProvider.Arr, "Results from Radarr and Sonarr")]
	public async Task Search_WithResults_SaysWhereTheyCameFrom(SearchProvider provider, string source)
	{
		_providerConfig
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					Sink = SinkKind.Arr,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = provider,
					AuthProvider = AuthProvider.Local,
					RegistrationMode = RegistrationMode.Open,
				}
			);
		var cut = Render<GlobalSearch>(parameters => parameters.Add(search => search.IsOpen, true));
		await cut.Find("#search-input").InputAsync(new ChangeEventArgs { Value = Movie });

		await cut.Find("#search-input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

		await Assert.That(cut.Find(".search-modal-content").TextContent).Contains(source);
	}

	[Test]
	public async Task Opening_AfterTheSearchProviderChanged_SaysTheNewSource()
	{
		var cut = Render<GlobalSearch>(parameters => parameters.Add(search => search.IsOpen, false));
		_providerConfig
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					Sink = SinkKind.Arr,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Arr,
					AuthProvider = AuthProvider.Local,
					RegistrationMode = RegistrationMode.Open,
				}
			);

		await cut.InvokeAsync(() => cut.Render(parameters => parameters.Add(search => search.IsOpen, true)));
		await cut.Find("#search-input").InputAsync(new ChangeEventArgs { Value = Movie });
		await cut.Find("#search-input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

		await Assert.That(cut.Find(".search-modal-content").TextContent).Contains("Results from Radarr and Sonarr");
	}

	[Test]
	public async Task Search_ForATitleAFeedPutOnTheWantlist_MarksItsToggleWithTheFeedsDot()
	{
		var origin = TestFeeds.Origin(new Guid(21, 0, 0, new byte[8]), new Guid(22, 0, 0, new byte[8]), "Documentaries");
		_wantlist
			.GetAsync(Any())
			.Returns(new WantlistSnapshot([new WantlistItem(Matched.Identity.Id, DateTimeOffset.UnixEpoch, new WantlistItemProvenance(origin))]));
		var cut = Render<GlobalSearch>(parameters => parameters.Add(search => search.IsOpen, true));
		await cut.Find("#search-input").InputAsync(new ChangeEventArgs { Value = Movie });

		await cut.Find("#search-input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

		await Assert.That(cut.FindAll(".search-result-row").Single(row => row.TextContent.Contains(Movie)).QuerySelectorAll(".toggle-dot")).IsNotEmpty();
	}
}
