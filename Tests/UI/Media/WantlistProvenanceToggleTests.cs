using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Shared.Media.Icons;
using UI.Shared.Services.Branding;

namespace UI.Tests.Media;

public class WantlistProvenanceToggleTests : BunitContext
{
	private static readonly MediaId Id = MediaId.From(new Guid(11, 0, 0, new byte[8]));

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_WhenWantlistedByAFeed_ColoursTheDotWithItsProvider()
	{
		var cut = RenderToggle(isToggled: true, item: FeedItem());
		var brand = Services.GetRequiredService<MediaProviderBrandingService>().GetBrandingInfo(MediaProvider.Mdblist).BrandColor;

		await Assert.That(cut.Find(".toggle-dot").GetAttribute("style")).IsEqualTo($"background: {brand}");
	}

	[Test]
	public async Task Render_WhileToggling_ShowsNoDot()
	{
		var cut = RenderToggle(isToggled: true, item: FeedItem(), isToggling: true);

		await Assert.That(cut.FindAll(".toggle-dot")).IsEmpty();
	}

	[Test]
	public async Task Render_WhenAddedByHand_ShowsANeutralDot()
	{
		var cut = RenderToggle(
			isToggled: true,
			item: new WantlistItem(Id, DateTimeOffset.UnixEpoch, new WantlistItemProvenance(new ManualOrigin(ManualSource.Search)))
		);

		await Assert.That(cut.Find(".toggle-dot").GetAttribute("style")).IsEqualTo("background: var(--muted-foreground)");
	}

	[Test]
	public async Task Render_WhenPulledFromTheSink_ColoursTheDotWithItsBrand()
	{
		var cut = RenderToggle(isToggled: true, item: new WantlistItem(Id, DateTimeOffset.UnixEpoch, new WantlistItemProvenance(new SinkOrigin(SinkKind.Plex))));
		var brand = Services.GetRequiredService<MediaProviderBrandingService>().GetBrandingInfo(MediaProvider.Plex).BrandColor;

		await Assert.That(cut.Find(".toggle-dot").GetAttribute("style")).IsEqualTo($"background: {brand}");
	}

	[Test]
	public async Task Render_WhenItsFeedWasDeleted_ShowsAMutedDot()
	{
		var cut = RenderToggle(isToggled: true, item: new WantlistItem(Id, DateTimeOffset.UnixEpoch, new WantlistItemProvenance(new OrphanedOrigin("Old feed"))));

		await Assert.That(cut.Find(".toggle-dot").GetAttribute("style")).IsEqualTo("background: var(--muted-foreground)");
	}

	[Test]
	public async Task Render_WhenNotWantlisted_ShowsNoDot()
	{
		var cut = RenderToggle(isToggled: false, item: null);

		await Assert.That(cut.FindAll(".toggle-dot")).IsEmpty();
	}

	[Test]
	public async Task RightClick_WhenWantlisted_RevealsTheProvenanceDetails()
	{
		var cut = RenderToggle(isToggled: true, item: FeedItem());

		await cut.Find(".provenance-trigger").ContextMenuAsync(new());

		await Assert.That(cut.Find(".provenance-details").TextContent).Contains("Documentaries");
	}

	[Test]
	public async Task RightClick_WhenNotWantlisted_RevealsNothing()
	{
		var cut = RenderToggle(isToggled: false, item: null);

		await cut.Find(".provenance-trigger").ContextMenuAsync(new());

		await Assert.That(cut.FindAll(".provenance-details")).IsEmpty();
	}

	[Test]
	public async Task Click_OnTheProvenanceDot_RevealsTheDetailsWithoutToggling()
	{
		MatchedMedia? toggled = null;
		var cut = RenderToggle(isToggled: true, item: FeedItem(), onToggle: value => toggled = value);

		await cut.Find("button.provenance-dot").ClickAsync(new());

		await Assert.That(cut.Find(".provenance-details").TextContent).Contains("Documentaries");
		await Assert.That(toggled).IsNull();
	}

	[Test]
	public async Task Click_OnTheBookmark_TogglesWithoutRevealing()
	{
		MatchedMedia? toggled = null;
		var cut = RenderToggle(isToggled: true, item: FeedItem(), onToggle: value => toggled = value);

		await cut.Find("button.search-result-toggle-btn").ClickAsync(new());

		await Assert.That(toggled).IsNotNull();
		await Assert.That(cut.FindAll(".provenance-details")).IsEmpty();
	}

	[Test]
	public async Task Reveal_WhenTheTitleLeavesAndReturnsWhileOpen_StaysClosed()
	{
		var cut = RenderToggle(isToggled: true, item: FeedItem());
		await cut.Find("button.provenance-dot").ClickAsync(new());

		cut.Render(parameters => parameters.Add(harness => harness.IsToggled, false));
		cut.Render(parameters => parameters.Add(harness => harness.IsToggled, true));

		await Assert.That(cut.FindAll(".provenance-details")).IsEmpty();
	}

	private IRenderedComponent<Harness> RenderToggle(bool isToggled, WantlistItem? item, bool isToggling = false, Action<MatchedMedia>? onToggle = null)
	{
		return Render<Harness>(parameters =>
			parameters
				.Add(harness => harness.IsToggled, isToggled)
				.Add(harness => harness.IsToggling, isToggling)
				.Add(harness => harness.Item, item)
				.Add(harness => harness.OnToggle, onToggle ?? (_ => { }))
		);
	}

	private sealed class Harness : ComponentBase
	{
		[Parameter]
		public bool IsToggled { get; set; }

		[Parameter]
		public bool IsToggling { get; set; }

		[Parameter]
		public WantlistItem? Item { get; set; }

		[Parameter]
		public EventCallback<MatchedMedia> OnToggle { get; set; }

		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<WantlistProvenanceToggle>(1);
			builder.AddAttribute(2, nameof(WantlistProvenanceToggle.Result), Movie());
			builder.AddAttribute(3, nameof(WantlistProvenanceToggle.IsToggled), IsToggled);
			builder.AddAttribute(4, nameof(WantlistProvenanceToggle.IsToggling), IsToggling);
			builder.AddAttribute(5, nameof(WantlistProvenanceToggle.Item), Optional.Of(Item));
			builder.AddAttribute(6, nameof(WantlistProvenanceToggle.OnToggle), OnToggle);
			builder.CloseComponent();
		}
	}

	private static WantlistItem FeedItem() =>
		new(
			Id,
			DateTimeOffset.UnixEpoch,
			new WantlistItemProvenance(TestFeeds.Origin(new Guid(21, 0, 0, new byte[8]), new Guid(22, 0, 0, new byte[8]), "Documentaries"))
		);

	private static MatchedMedia Movie() => new(new MediaIdentity(Id, []), new MovieDetails());
}
