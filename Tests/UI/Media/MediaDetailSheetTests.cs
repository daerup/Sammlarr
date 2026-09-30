using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives.Services;
using Bunit;
using Contracts.Blocklist;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using Contracts.Wantlist.Outbox;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using Domain.Settings;
using Domain.Wantlist;
using Domain.Wantlist.Outbox;
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

	private Mock<IWantlistService> _wantlist = null!;

	[Test]
	public async Task Toggle_WhenARemovalIsAlreadyQueued_StillReadsAsAddRatherThanCancel()
	{
		Register();
		_wantlist.GetWantlistIdsAsync(Any()).ReturnsSequentially(Owned, Empty, Empty);

		var cut = RenderSheet();
		cut.WaitForState(() => Toggle(cut) is not null);
		await Toggle(cut)!.ClickAsync(new());

		await Assert.That(Toggle(cut)!.GetAttribute("aria-label")).IsEqualTo("Add to wantlist");
	}

	[Test]
	public async Task Dot_WhenTheItemCameFromAFeed_UsesTheFeedProvidersBrand()
	{
		Register();
		_wantlist.GetWantlistIdsAsync(Any()).Returns(Owned);
		_wantlist.GetEntryAsync(Any(), Any()).Returns(Some.Of(Entry(TestFeeds.Origin(Guid.NewGuid(), Guid.NewGuid(), "feed"))));

		var cut = RenderSheet();
		cut.WaitForState(() => cut.FindAll(".toggle-dot").Count > 0);

		await Assert.That(cut.Find(".toggle-dot").GetAttribute("style")).IsNotNull();
	}

	[Test]
	public async Task Dot_WhenTheItemWasAddedByHand_IsNotShown()
	{
		Register();
		_wantlist.GetWantlistIdsAsync(Any()).Returns(Owned);
		_wantlist.GetEntryAsync(Any(), Any()).Returns(Some.Of(Entry(new ManualOrigin(ManualSource.Search))));

		var cut = RenderSheet();
		cut.WaitForState(() => Toggle(cut) is not null);

		await Assert.That(cut.FindAll(".toggle-dot")).IsEmpty();
	}

	[Test]
	public async Task Provenance_WhenTheWantlistIconIsRightClicked_IsRevealedInAPopover()
	{
		Register();
		_wantlist.GetWantlistIdsAsync(Any()).Returns(Owned);
		_wantlist.GetEntryAsync(Any(), Any()).Returns(Some.Of(Entry(TestFeeds.Origin(Guid.NewGuid(), Guid.NewGuid(), "Comedy"))));

		var cut = RenderSheet();
		cut.WaitForState(() => cut.FindAll(".provenance-trigger").Count > 0);

		await Assert.That(cut.FindAll(".provenance-details")).IsEmpty();

		await cut.Find(".provenance-trigger").ContextMenuAsync(new());

		var details = cut.Find(".provenance-details");
		var links = details.QuerySelectorAll("a").Select(link => link.GetAttribute("href")).ToList();

		await Assert.That(details.TextContent).Contains("Comedy");
		await Assert.That(links).HasCount(2);
		await Assert.That(links[0]).StartsWith("/feeds/edit/");
		await Assert.That(links[1]).Contains("/runs/");
		await Assert.That(links[1]).Contains("?item=");
	}

	[Test]
	public async Task Provenance_ForATitleThatIsNotOnTheWantlist_StaysHiddenOnRightClick()
	{
		Register();

		var cut = RenderSheet();
		cut.WaitForState(() => cut.FindAll(".provenance-trigger").Count > 0);

		await cut.Find(".provenance-trigger").ContextMenuAsync(new());

		await Assert.That(cut.FindAll(".provenance-details")).IsEmpty();
	}

	private static WantlistItem Entry(WantlistItemProvenance provenance) => new(Id, DateTimeOffset.UnixEpoch, provenance);

	private static IElement? Toggle(IRenderedComponent<Harness> cut) =>
		cut.FindAll(".media-detail-sheet-content button")
			.FirstOrDefault(button => button.GetAttribute("aria-label") is { } label && (label.Contains("wantlist", StringComparison.OrdinalIgnoreCase)));

	private IRenderedComponent<Harness> RenderSheet() => Render<Harness>();

	private static IReadOnlySet<MediaId> Owned => new HashSet<MediaId> { Id };

	private static IReadOnlySet<MediaId> Empty => new HashSet<MediaId>();

	private void Register()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddLogging();

		_wantlist = IWantlistService.Mock();
		_wantlist.GetWantlistIdsAsync(Any()).Returns(Empty);
		_wantlist.GetEntryAsync(Any(), Any()).Returns(new None());
		Services.AddSingleton(_wantlist.Object);

		var blocklist = IBlocklistService.Mock();
		blocklist.GetBlocklistIdsAsync(Any()).Returns(Empty);
		Services.AddSingleton(blocklist.Object);

		Services.AddSingleton(IMediaService.Mock().Object);

		var providerConfig = IProviderConfigService.Mock();
		providerConfig
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					Sink = SinkKind.Plex,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Plex,
					RatingProvider = RatingProvider.Tmdb,
					AuthProvider = AuthProvider.Plex,
					RegistrationMode = RegistrationMode.Open,
				}
			);
		Services.AddSingleton(providerConfig.Object);

		Services.AddScoped<Notifier>();
		Services.AddSingleton<MediaProviderBrandingService>();
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
