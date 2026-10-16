using BlazorBlueprint.Components;
using Bunit;
using Contracts.Announcing;
using Contracts.Blocklist;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Media.Search;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Blocklist;
using Domain.Identity;
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

namespace UI.Tests.Media;

public class GlobalSearchTests : BunitContext
{
	private const string Movie = "Inception";
	private const string Person = "Christopher Nolan";

	private static readonly MatchedMedia Matched = new(new MediaIdentity(MediaId.New(), [PlexId.From("1", MediaType.Movie)]), new MovieDetails { Title = Movie });

	private static readonly WantlistSnapshot WithTheMovie = new([
		new WantlistItem(Matched.Identity.Id, DateTimeOffset.UnixEpoch, new WantlistItemProvenance(new ManualOrigin(ManualSource.Search))),
	]);

	private static readonly MediaMatchResult Unmatched = new UnmatchedMedia([PlexId.From("123", MediaType.Unknown)], new UnknownDetails { Title = Person });

	private readonly Mock<IWantlistService> _wantlist = IWantlistService.Mock();

	private readonly Mock<IProviderConfigService> _providerConfig = IProviderConfigService.Mock();

	private readonly TestEventPublisher _events = new();

	private readonly Mock<IBlocklistService> _blocklist = IBlocklistService.Mock();

	private readonly Mock<IMediaSearchService> _search = IMediaSearchService.Mock();

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton<IEventPublisher>(_events);
		Services.AddSingleton<ICurrentUser>(new TestCurrentUser());
		Services.AddSingleton(RatingStub.Context());
		Services.AddLogging();
		Services.AddScoped<Notifier>();
		Services.AddProviderBranding();
		Services.AddMediaTitleLink();
		Services.AddSingleton(ArtworkStub.Sizing());
		Services.AddSingleton(IMediaService.Mock().Object);

		_providerConfig.GetAsync(Any()).Returns(ProviderBranding.OnPlexSearchingPlex);
		Services.AddSingleton(_providerConfig.Object);

		_wantlist.GetAsync(Any()).Returns(new WantlistSnapshot([]));
		Services.AddSingleton(_wantlist.Object);

		_blocklist.GetAsync(Any()).Returns(new BlocklistSnapshot([]));
		Services.AddSingleton(_blocklist.Object);

		_search.SearchAsync(Any(), Any(), Any()).Returns(new SearchResults([Matched, Unmatched], []));
		Services.AddSingleton(_search.Object);

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
	[Arguments(SearchProvider.Tmdb, "Results from The Movie Database")]
	[Arguments(SearchProvider.Arr, "Results from Radarr and Sonarr")]
	[Arguments(SearchProvider.Plex, "Results from Plex")]
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
	public async Task Search_OnRadarrAndSonarr_SplitsMoviesAndShowsIntoTabsOpeningTheLarger()
	{
		SearchWith(SearchProvider.Arr);
		_search
			.SearchAsync(Any(), Any(), Any())
			.Returns(new SearchResults([Found("Dune: Prophecy", MediaType.Show), Found("Dune", MediaType.Movie), Found("Dune: Part Two", MediaType.Movie)], []));
		var cut = Render<GlobalSearch>(parameters => parameters.Add(search => search.IsOpen, true));
		await cut.Find("#search-input").InputAsync(new ChangeEventArgs { Value = "dune" });
		await cut.Find("#search-input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

		await Assert.That(cut.FindAll("[role=tab]").Select(tab => tab.TextContent.Squeezed())).IsEquivalentTo(["Movies2", "Shows1"]);
		await Assert.That(cut.FindAll(".search-result-row")).Count().IsEqualTo(2);

		await cut.Find("[role=tab][aria-selected=false]").ClickAsync(new MouseEventArgs());

		await Assert.That(cut.FindAll(".search-result-row").Single().TextContent).Contains("Dune: Prophecy");
	}

	[Test]
	public async Task Search_WhenSonarrMissed_SaysItsShowsAreMissing()
	{
		SearchWith(SearchProvider.Arr);
		_search
			.SearchAsync(Any(), Any(), Any())
			.Returns(new SearchResults([Found("Dune", MediaType.Movie)], [new SearchMiss(RemoteService.Sonarr, "connection refused")]));
		var cut = Render<GlobalSearch>(parameters => parameters.Add(search => search.IsOpen, true));
		await cut.Find("#search-input").InputAsync(new ChangeEventArgs { Value = "dune" });
		await cut.Find("#search-input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

		var miss = cut.Find("[data-testid=search-miss]").TextContent;
		await Assert.That(miss).Contains("Sonarr");
		await Assert.That(miss).Contains("shows");
		await Assert.That(cut.Find("[data-testid=search-miss-reason]").TextContent).IsEqualTo("connection refused");
	}

	[Test]
	public async Task Search_OnTmdb_ListsEveryResultWithoutTabs()
	{
		SearchWith(SearchProvider.Tmdb);
		var cut = Render<GlobalSearch>(parameters => parameters.Add(search => search.IsOpen, true));
		await cut.Find("#search-input").InputAsync(new ChangeEventArgs { Value = Movie });
		await cut.Find("#search-input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

		await Assert.That(cut.FindAll("[role=tab]")).IsEmpty();
		await Assert.That(cut.FindAll(".search-result-row")).Count().IsEqualTo(2);
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

	[Test]
	public async Task Adding_ATitleTheSinkHasNoMatchFor_WarnsInsteadOfFailing()
	{
		_wantlist.AddAsync(Any(), Any(), Any()).Returns(new WantlistAdded([], [Matched.Identity.Id]));
		var cut = Render<GlobalSearch>(parameters => parameters.Add(search => search.IsOpen, true));
		await cut.Find("#search-input").InputAsync(new ChangeEventArgs { Value = Movie });
		await cut.Find("#search-input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

		await cut.Find("button[aria-label='Add to wantlist']").ClickAsync(new MouseEventArgs());

		var toast = Services.GetRequiredService<ToastService>().Toasts.Single();
		await Assert.That(toast.Variant).IsEqualTo(ToastVariant.Warning);
		await Assert.That(toast.Description).Contains("Plex");
		await Assert.That(toast.Description).Contains(Movie);
	}

	[Test]
	public async Task Wantlist_WhenTheViewerAddsATitleElsewhere_MarksItsToggle()
	{
		_wantlist.GetAsync(Any()).ReturnsSequentially(new WantlistSnapshot([]), WithTheMovie);
		var cut = await SearchForTheMovieAsync();

		await cut.InvokeAsync(() => _events.Publish(new WantlistChangedEvent(TestCurrentUser.DefaultId, [Matched.Identity.Id], [])));
		cut.WaitForState(() => cut.FindAll(".provenance-trigger > .search-result-toggle-btn[aria-label='Remove from wantlist']").Count == 1);

		await Assert.That(WantlistToggleLabel(cut)).IsEqualTo("Remove from wantlist");
	}

	[Test]
	public async Task Wantlist_WhenAnotherPersonAddsATitle_LeavesItsToggleAlone()
	{
		_wantlist.GetAsync(Any()).ReturnsSequentially(new WantlistSnapshot([]), WithTheMovie);
		var cut = await SearchForTheMovieAsync();

		await cut.InvokeAsync(() => _events.Publish(new WantlistChangedEvent(UserId.New(), [Matched.Identity.Id], [])));

		await Assert.That(WantlistToggleLabel(cut)).IsEqualTo("Add to wantlist");
	}

	[Test]
	public async Task Wantlist_WhenTheChangeIsAlreadyShown_LeavesItsToggleAlone()
	{
		_wantlist.GetAsync(Any()).ReturnsSequentially(WithTheMovie, new WantlistSnapshot([]));
		var cut = await SearchForTheMovieAsync();

		await cut.InvokeAsync(() => _events.Publish(new WantlistChangedEvent(TestCurrentUser.DefaultId, [Matched.Identity.Id], [])));

		await Assert.That(WantlistToggleLabel(cut)).IsEqualTo("Remove from wantlist");
	}

	[Test]
	public async Task Purpose_WhenBlocklist_TitlesTheSearchAndBlocklistsTheTitle()
	{
		var cut = Render<GlobalSearch>(parameters => parameters.Add(search => search.IsOpen, true).Add(search => search.Purpose, SearchPurpose.Blocklist));
		await cut.Find("#search-input").InputAsync(new ChangeEventArgs { Value = Movie });
		await cut.Find("#search-input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

		await cut.Find("button[aria-label='Add to blocklist']").ClickAsync(new MouseEventArgs());

		await Assert.That(cut.Find(".search-modal").GetAttribute("aria-label")).IsEqualTo("Blocklist a title");
		_blocklist.AddAsync(Any(), Any()).WasCalled(Times.Once);
		_wantlist.AddAsync(Any(), Any(), Any()).WasNeverCalled();
	}

	private void SearchWith(SearchProvider provider)
	{
		_providerConfig
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					Sink = SinkKind.Plex,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = provider,
					AuthProvider = AuthProvider.Local,
					RegistrationMode = RegistrationMode.Open,
				}
			);
	}

	private async Task<IRenderedComponent<GlobalSearch>> SearchForTheMovieAsync()
	{
		var cut = Render<GlobalSearch>(parameters => parameters.Add(search => search.IsOpen, true));
		await cut.Find("#search-input").InputAsync(new ChangeEventArgs { Value = Movie });
		await cut.Find("#search-input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
		return cut;
	}

	private static string? WantlistToggleLabel(IRenderedComponent<GlobalSearch> cut)
	{
		return cut.Find(".provenance-trigger > .search-result-toggle-btn").GetAttribute("aria-label");
	}

	private static MediaMatchResult Found(string title, MediaType type)
	{
		var identity = new MediaIdentity(MediaId.New(), [TmdbId.From(title.Length.ToString(), type)]);
		return new MatchedMedia(identity, type == MediaType.Movie ? new MovieDetails { Title = title } : new ShowDetails { Title = title });
	}
}
