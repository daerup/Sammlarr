using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives.Services;
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
		Services.AddSingleton<MediaProviderBrandingService>();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_WhenWantlistedByAFeed_ShowsTheProvenanceDot()
	{
		var cut = RenderToggle(isToggled: true, item: FeedItem());

		await Assert.That(cut.FindAll(".toggle-dot").Count).IsEqualTo(1);
	}

	[Test]
	public async Task Render_WhenAddedByHand_ShowsNoDotBecauseThereIsNoProviderToColourIt()
	{
		var cut = RenderToggle(
			isToggled: true,
			item: new WantlistItem(Id, DateTimeOffset.UnixEpoch, new WantlistItemProvenance(new ManualOrigin(ManualSource.Search)))
		);

		await Assert.That(cut.FindAll(".toggle-dot")).IsEmpty();
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
	public async Task Click_WhenTheIconIsPressed_StillTogglesThroughToTheCaller()
	{
		MatchedMedia? toggled = null;
		var cut = Render<WantlistProvenanceToggle>(parameters =>
			parameters.Add(toggle => toggle.Result, Movie()).Add(toggle => toggle.OnToggle, value => toggled = value)
		);

		await cut.Find("button.search-result-toggle-btn").ClickAsync();

		await Assert.That(toggled).IsNotNull();
	}

	private IRenderedComponent<Harness> RenderToggle(bool isToggled, WantlistItem? item) =>
		Render<Harness>(parameters => parameters.Add(harness => harness.IsToggled, isToggled).Add(harness => harness.Item, item));

	private sealed class Harness : ComponentBase
	{
		[Parameter]
		public bool IsToggled { get; set; }

		[Parameter]
		public WantlistItem? Item { get; set; }

		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<WantlistProvenanceToggle>(1);
			builder.AddAttribute(2, nameof(WantlistProvenanceToggle.Result), Movie());
			builder.AddAttribute(3, nameof(WantlistProvenanceToggle.IsToggled), IsToggled);
			builder.AddAttribute(4, nameof(WantlistProvenanceToggle.Item), Optional.Of(Item));
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
