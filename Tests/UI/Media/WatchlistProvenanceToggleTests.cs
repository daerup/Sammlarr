using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives.Services;
using Bunit;
using Domain.Media;
using Domain.Watchlist;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Shared.Media.Icons;
using UI.Shared.Services.Branding;

namespace UI.Tests.Media;

public class WatchlistProvenanceToggleTests : BunitContext
{
	private static readonly MediaId Id = MediaId.From(new Guid(11, 0, 0, new byte[8]));

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddScoped(_ => new MediaProviderBrandingService(string.Empty));
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_WhenWatchlistedByAFeed_ShowsTheProvenanceDot()
	{
		var cut = RenderToggle(isToggled: true, entry: FeedEntry());

		await Assert.That(cut.FindAll(".toggle-dot").Count).IsEqualTo(1);
	}

	[Test]
	public async Task Render_WhenAddedByHand_ShowsNoDotBecauseThereIsNoProviderToColourIt()
	{
		var cut = RenderToggle(
			isToggled: true,
			entry: new WatchlistEntry(Id, DateTimeOffset.UnixEpoch, new WatchlistItemProvenance(new ManualOrigin(ManualSource.Search)))
		);

		await Assert.That(cut.FindAll(".toggle-dot")).IsEmpty();
	}

	[Test]
	public async Task Render_WhenNotWatchlisted_ShowsNoDot()
	{
		var cut = RenderToggle(isToggled: false, entry: null);

		await Assert.That(cut.FindAll(".toggle-dot")).IsEmpty();
	}

	[Test]
	public async Task RightClick_WhenWatchlisted_RevealsTheProvenanceDetails()
	{
		var cut = RenderToggle(isToggled: true, entry: FeedEntry());

		await cut.Find(".provenance-trigger").ContextMenuAsync(new());

		await Assert.That(cut.Find(".provenance-details").TextContent).Contains("Documentaries");
	}

	[Test]
	public async Task RightClick_WhenNotWatchlisted_RevealsNothing()
	{
		var cut = RenderToggle(isToggled: false, entry: null);

		await cut.Find(".provenance-trigger").ContextMenuAsync(new());

		await Assert.That(cut.FindAll(".provenance-details")).IsEmpty();
	}

	[Test]
	public async Task Click_WhenTheIconIsPressed_StillTogglesThroughToTheCaller()
	{
		MediaLookup? toggled = null;
		var cut = Render<WatchlistProvenanceToggle>(parameters =>
			parameters.Add(toggle => toggle.Result, Lookup()).Add(toggle => toggle.OnToggle, value => toggled = value)
		);

		await cut.Find("button.search-result-toggle-btn").ClickAsync();

		await Assert.That(toggled).IsNotNull();
	}

	private IRenderedComponent<Harness> RenderToggle(bool isToggled, WatchlistEntry? entry) =>
		Render<Harness>(parameters => parameters.Add(harness => harness.IsToggled, isToggled).Add(harness => harness.Entry, entry));

	private sealed class Harness : ComponentBase
	{
		[Parameter]
		public bool IsToggled { get; set; }

		[Parameter]
		public WatchlistEntry? Entry { get; set; }

		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<WatchlistProvenanceToggle>(1);
			builder.AddAttribute(2, nameof(WatchlistProvenanceToggle.Result), Lookup());
			builder.AddAttribute(3, nameof(WatchlistProvenanceToggle.IsToggled), IsToggled);
			builder.AddAttribute(4, nameof(WatchlistProvenanceToggle.Entry), Entry);
			builder.CloseComponent();
		}
	}

	private static WatchlistEntry FeedEntry() =>
		new(Id, DateTimeOffset.UnixEpoch, new WatchlistItemProvenance(TestFeeds.Origin(new Guid(21, 0, 0, new byte[8]), Guid.Empty, "Documentaries")));

	private static MediaLookup Lookup() => new PartialMovie { Identity = new MediaIdentity(Id, []) }.ToMatch();
}
