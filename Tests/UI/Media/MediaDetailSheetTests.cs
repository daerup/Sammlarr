using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives.Services;
using Bunit;
using Contracts.Blacklist;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using Domain.Settings;
using Domain.Watchlist;
using Domain.Watchlist.Outbox;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Shared.Media;
using UI.Shared.Services;
using UI.Shared.Services.Branding;

namespace UI.Tests.Media;

public class MediaDetailSheetTests : BunitContext
{
	private static readonly MediaId Id = MediaId.From(new Guid(21, 0, 0, new byte[8]));

	private Mock<IWatchlistService> _watchlist = null!;

	[Test]
	public async Task Toggle_WhenARemovalIsAlreadyQueued_StillReadsAsAddRatherThanCancel()
	{
		Register();
		_watchlist.GetWatchlistIdsAsync(Any()).ReturnsSequentially(Owned, Empty, Empty);

		var cut = RenderSheet();
		cut.WaitForState(() => Toggle(cut) is not null);
		await Toggle(cut)!.ClickAsync(new());

		await Assert.That(Toggle(cut)!.GetAttribute("aria-label")).IsEqualTo("Add to watchlist");
	}

	[Test]
	public async Task Dot_WhenTheItemCameFromAFeed_UsesTheFeedProvidersBrand()
	{
		Register();
		_watchlist.GetWatchlistIdsAsync(Any()).Returns(Owned);
		_watchlist.GetEntryAsync(Any(), Any()).Returns(Some.Of(Entry(TestFeeds.Origin(Guid.NewGuid(), Guid.NewGuid(), "feed"))));

		var cut = RenderSheet();
		cut.WaitForState(() => cut.FindAll(".toggle-dot").Count > 0);

		await Assert.That(cut.Find(".toggle-dot").GetAttribute("style")).IsNotNull();
	}

	[Test]
	public async Task Dot_WhenTheItemWasAddedByHand_IsNotShown()
	{
		Register();
		_watchlist.GetWatchlistIdsAsync(Any()).Returns(Owned);
		_watchlist.GetEntryAsync(Any(), Any()).Returns(Some.Of(Entry(new ManualOrigin(ManualSource.Search))));

		var cut = RenderSheet();
		cut.WaitForState(() => Toggle(cut) is not null);

		await Assert.That(cut.FindAll(".toggle-dot")).IsEmpty();
	}

	[Test]
	public async Task Provenance_WhenTheWatchlistIconIsRightClicked_IsRevealedInAPopover()
	{
		Register();
		_watchlist.GetWatchlistIdsAsync(Any()).Returns(Owned);
		_watchlist.GetEntryAsync(Any(), Any()).Returns(Some.Of(Entry(TestFeeds.Origin(Guid.NewGuid(), Guid.NewGuid(), "Comedy"))));

		var cut = RenderSheet();
		cut.WaitForState(() => cut.FindAll(".media-sheet-provenance-trigger").Count > 0);

		await Assert.That(cut.FindAll(".provenance-details")).IsEmpty();

		await cut.Find(".media-sheet-provenance-trigger").ContextMenuAsync(new());

		var details = cut.Find(".provenance-details");
		var links = details.QuerySelectorAll("a").Select(link => link.GetAttribute("href")).ToList();

		await Assert.That(details.TextContent).Contains("Comedy");
		await Assert.That(links).HasCount(2);
		await Assert.That(links[0]).StartsWith("/feeds/edit/");
		await Assert.That(links[1]).Contains("/runs/");
		await Assert.That(links[1]).Contains("?item=");
	}

	[Test]
	public async Task Provenance_ForATitleThatIsNotOnTheWatchlist_StaysHiddenOnRightClick()
	{
		Register();

		var cut = RenderSheet();
		cut.WaitForState(() => cut.FindAll(".media-sheet-provenance-trigger").Count > 0);

		await cut.Find(".media-sheet-provenance-trigger").ContextMenuAsync(new());

		await Assert.That(cut.FindAll(".provenance-details")).IsEmpty();
	}

	private static WatchlistEntry Entry(WatchlistItemProvenance provenance) => new(Id, DateTimeOffset.UnixEpoch, provenance);

	private static IElement? Toggle(IRenderedComponent<Harness> cut) =>
		cut.FindAll(".media-detail-sheet-content button")
			.FirstOrDefault(button => button.GetAttribute("aria-label") is { } label && (label.Contains("watchlist", StringComparison.OrdinalIgnoreCase)));

	private IRenderedComponent<Harness> RenderSheet() => Render<Harness>();

	private static IReadOnlySet<MediaId> Owned => new HashSet<MediaId> { Id };

	private static IReadOnlySet<MediaId> Empty => new HashSet<MediaId>();

	private void Register()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddLogging();

		_watchlist = IWatchlistService.Mock();
		_watchlist.GetWatchlistIdsAsync(Any()).Returns(Empty);
		_watchlist.GetEntryAsync(Any(), Any()).Returns(new None());
		Services.AddSingleton(_watchlist.Object);

		var blacklist = IBlacklistService.Mock();
		blacklist.GetBlacklistIdsAsync(Any()).Returns(Empty);
		Services.AddSingleton(blacklist.Object);

		Services.AddSingleton(IMediaService.Mock().Object);

		var providerConfig = IProviderConfigRepository.Mock();
		providerConfig
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					WatchlistProvider = WatchlistProvider.Plex,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Plex,
					RatingProvider = RatingProvider.Tmdb,
					AuthProvider = AuthProvider.Plex,
					RegistrationMode = RegistrationMode.Open,
				}
			);
		Services.AddSingleton(providerConfig.Object);

		Services.AddScoped<Notifier>();
		Services.AddScoped(_ => new MediaProviderBrandingService(string.Empty));
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	private sealed class Harness : ComponentBase
	{
		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<MediaDetailSheet>(1);
			builder.AddAttribute(2, nameof(MediaDetailSheet.Open), true);
			builder.AddAttribute(3, nameof(MediaDetailSheet.Media), (MediaLookup?)Detail());
			builder.CloseComponent();
		}
	}

	private static IdentifiedMedia Detail() =>
		new PartialMovie
		{
			Identity = new MediaIdentity(Id, [TmdbId.From(Id.Value.ToString(), MediaType.Movie)]),
			Title = "Futurama",
			Tagline = string.Empty,
			Summary = "A delivery boy is frozen.",
			PosterUrl = string.Empty,
			BackdropUrl = string.Empty,
			ReleaseDate = new DateOnly(1999, 3, 28),
			Genres = [],
			Runtime = TimeSpan.FromMinutes(22),
			Rating = RatingSummary.None,
		};
}
