using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts.Blocklist;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Blocklist;
using Domain.Media;
using Domain.Wantlist;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Shared.Layout;
using UI.Shared.Media;
using UI.Shared.Services;

namespace UI.Tests.Media;

public class MediaDetailSheetTests : BunitContext
{
	private const string Poster = "https://cdn.example.test/poster.jpg";

	private static readonly MediaId Id = MediaId.From(new Guid(21, 0, 0, new byte[8]));

	private Mock<IWantlistService> _wantlist = null!;

	private Mock<IMediaService> _media = null!;

	private Mock<IBlocklistService> _blocklist = null!;

	[Test]
	public async Task Toggle_WhenARemovalIsAlreadyQueued_StillReadsAsAddRatherThanCancel()
	{
		Register();
		_wantlist.GetAsync(Any()).ReturnsSequentially(Wantlisted(new ManualOrigin(ManualSource.Search)), Nothing, Nothing);

		var cut = RenderSheet();
		cut.WaitForState(() => Toggle(cut) is not null);
		await Toggle(cut)!.ClickAsync(new());

		await Assert.That(Toggle(cut)!.GetAttribute("aria-label")).IsEqualTo("Add to wantlist");
	}

	[Test]
	public async Task Rating_WhenTheSheetOpensOnAPartlyKnownTitle_LoadsEverythingAndShowsItsRatings()
	{
		Register();
		var loaded = new MatchedMedia(
			new MediaIdentity(Id, []),
			new MovieDetails { Title = "Futurama", Ratings = MediaRatings.From([new TmdbRating(7.4f, Some.Of(900))]) }
		);
		_media
			.GetDetailsAsync(Any<IEnumerable<MediaId>>(), FieldNeeds.Everything, Any())
			.Returns(new[] { (Result<MatchedMedia>)Success.Of(loaded) }.ToAsyncEnumerable());

		var cut = RenderSheet();
		cut.WaitForState(() => cut.FindAll(".rating-badge").Count > 0);

		await Assert.That(cut.Find(".rating-badge").TextContent.Trim()).IsEqualTo("7.4");
	}

	[Test]
	public async Task Provenance_WhenTheWantlistIconIsRightClicked_IsRevealedInAPopover()
	{
		Register();
		_wantlist.GetAsync(Any()).Returns(Wantlisted(TestFeeds.Origin(Guid.NewGuid(), Guid.NewGuid())));

		var cut = RenderSheet();
		cut.WaitForState(() => cut.FindAll(".provenance-trigger").Count > 0);

		await Assert.That(cut.FindAll(".provenance-details")).IsEmpty();

		await cut.Find(".provenance-trigger").ContextMenuAsync(new());

		var details = cut.Find(".provenance-details");
		var links = details.QuerySelectorAll("a").Select(link => link.GetAttribute("href")).ToList();

		await Assert.That(details.TextContent).Contains("Comedy");
		await Assert.That(links).Count().IsEqualTo(2);
		await Assert.That(links[0]).StartsWith("/feeds/edit/");
		await Assert.That(links[1]).Contains("/runs/");
		await Assert.That(links[1]).Contains("?item=");
	}

	[Test]
	public async Task Hero_WithAPoster_LaysABlurredBandOfItBehind()
	{
		Register();

		var cut = RenderSheet(Detail(poster: Poster));

		await Assert.That(cut.Find(".hero-band img").GetAttribute("src")).IsEqualTo(Poster);
		await Assert.That(cut.Find(".hero-band").GetAttribute("aria-hidden")).IsEqualTo("true");
	}

	[Test]
	public async Task Hero_WithoutAPoster_HasNoBand()
	{
		Register();

		var cut = RenderSheet();

		await Assert.That(cut.FindAll(".hero-band")).IsEmpty();
	}

	[Test]
	public async Task Sheet_OnAPhone_RisesFromTheBottomWithAHandle()
	{
		Register();
		RenderTree.TryAdd<CascadingValue<bool>>(cascade => cascade.Add(value => value.Name, PhoneLayout.IsPhoneCascade).Add(value => value.Value, true));

		var cut = RenderSheet();

		var sheet = cut.Find(".media-detail-sheet-content");
		await Assert.That(sheet.HasAttribute("data-sheet")).IsTrue();
		await Assert.That(sheet.GetAttribute("data-side")).IsEqualTo("bottom");
		await Assert.That(sheet.QuerySelectorAll("[data-sheet-handle]")).HasSingleItem();
	}

	[Test]
	public async Task Unmatched_SaysWhyItCannotBeWantlisted()
	{
		Register();

		var cut = RenderSheet(new UnmatchedMedia([], new MovieDetails { Title = "Unsung Hero" }));

		await Assert.That(cut.Find(".media-sheet-unmatched").TextContent.Trim()).IsEqualTo(WantlistNotices.Unmatched);
		await Assert.That(Toggle(cut)).IsNull();
	}

	[Test]
	public async Task Blocklist_ForATitleStillLoading_WarnsThatItIsNotReady()
	{
		Register();
		_blocklist.AddAsync(Any(), Any()).Throws(new UnknownMediaException(Id));

		var cut = RenderSheet();
		cut.WaitForState(() => cut.FindAll(".media-detail-sheet-content button[aria-label='Add to blocklist']").Count == 1);
		await cut.Find(".media-detail-sheet-content button[aria-label='Add to blocklist']").ClickAsync(new());

		var toast = Services.GetRequiredService<ToastService>().Toasts.Single();
		await Assert.That(toast.Variant).IsEqualTo(ToastVariant.Warning);
		await Assert.That(toast.Description).IsEqualTo("'Futurama' isn't ready yet: still loading.");
	}

	private static WantlistSnapshot Wantlisted(WantlistItemProvenance provenance) => new([new WantlistItem(Id, DateTimeOffset.UnixEpoch, provenance)]);

	private static IElement? Toggle(IRenderedComponent<Harness> cut) =>
		cut.FindAll(".media-detail-sheet-content button")
			.FirstOrDefault(button => button.GetAttribute("aria-label") is { } label && label.Contains("wantlist", StringComparison.OrdinalIgnoreCase));

	private IRenderedComponent<Harness> RenderSheet(MediaMatchResult? media = null)
	{
		return Render<Harness>(parameters => parameters.Add(harness => harness.Media, media ?? Detail()));
	}

	private static WantlistSnapshot Nothing => new([]);

	private void Register()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(RatingStub.Context());
		Services.AddLogging();

		_wantlist = IWantlistService.Mock();
		_wantlist.GetAsync(Any()).Returns(Nothing);
		Services.AddSingleton(_wantlist.Object);

		_blocklist = IBlocklistService.Mock();
		_blocklist.GetAsync(Any()).Returns(new BlocklistSnapshot([]));
		Services.AddSingleton(_blocklist.Object);

		_media = IMediaService.Mock();
		_media.GetDetailsAsync(Any<IEnumerable<MediaId>>(), Any(), Any()).Returns(AsyncEnumerable.Empty<Result<MatchedMedia>>());
		Services.AddSingleton(_media.Object);

		var providerConfig = IProviderConfigService.Mock();
		providerConfig.GetAsync(Any()).Returns(ProviderBranding.OnPlexSearchingPlex);
		Services.AddSingleton(providerConfig.Object);

		Services.AddScoped<Notifier>();
		Services.AddProviderBranding();
		Services.AddMediaTitleLink();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	private sealed class Harness : ComponentBase
	{
		[Parameter]
		public required MediaMatchResult Media { get; set; }

		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<MediaDetailSheet>(1);
			builder.AddAttribute(2, nameof(MediaDetailSheet.Open), true);
			builder.AddAttribute(3, nameof(MediaDetailSheet.Media), Media);
			builder.CloseComponent();
		}
	}

	private static MediaMatchResult Detail(string? poster = null)
	{
		return new MatchedMedia(
			new MediaIdentity(Id, [TmdbId.From(Id.Value.ToString(), MediaType.Movie)]),
			new MovieDetails
			{
				Title = "Futurama",
				Summary = "A delivery boy is frozen.",
				ReleaseDate = new DateOnly(1999, 3, 28),
				Genres = [],
				Runtime = TimeSpan.FromMinutes(22),
				Ratings = MediaRatings.None,
				PosterUrl = poster,
			}
		);
	}
}
