using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Domain.Media;
using Domain.Wantlist;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using TestInfrastructure;
using UI.Shared.Media.Icons;

namespace UI.Tests.Media;

public class WantlistToggleTests : BunitContext
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
	public async Task Click_WhenTheBookmarkIsPressed_InvokesTheToggleCallbackWithItsMedia()
	{
		var media = Movie();
		MatchedMedia? toggled = null;
		var cut = Render<WantlistToggle>(parameters => parameters.Add(toggle => toggle.Result, media).Add(toggle => toggle.OnToggle, value => toggled = value));

		await cut.Find("button").ClickAsync();

		await Assert.That(toggled).IsEqualTo(media);
	}

	[Test]
	[Arguments(true, "Remove from wantlist")]
	[Arguments(false, "Add to wantlist")]
	public async Task Render_ForEitherState_LabelsTheButtonWithWhatAClickDoes(bool toggled, string label)
	{
		var cut = Render<WantlistToggle>(parameters => parameters.Add(toggle => toggle.Result, Movie()).Add(toggle => toggle.IsToggled, toggled));

		await Assert.That(cut.Find("button").GetAttribute("aria-label")).IsEqualTo(label);
	}

	[Test]
	public async Task Render_WhileToggling_ShowsNoDot()
	{
		var cut = RenderToggle(isToggled: true, item: FeedItem(), isToggling: true);

		await Assert.That(cut.FindAll(".toggle-dot")).IsEmpty();
	}

	[Test]
	public async Task Render_WhenNotWantlisted_ShowsNoDot()
	{
		var cut = RenderToggle(isToggled: false, item: null);

		await Assert.That(cut.FindAll(".toggle-dot")).IsEmpty();
	}

	[Test]
	public async Task Render_WhenWantlisted_DrawsOneDotOnTheBookmarkIcon()
	{
		var cut = RenderToggle(isToggled: true, item: FeedItem());

		await Assert.That(cut.FindAll(".toggle-dot")).HasSingleItem();
		await Assert.That(cut.Find("button.search-result-toggle-btn").QuerySelectorAll(".toggle-dot")).HasSingleItem();
	}

	[Test]
	public async Task Render_WhenReadOnly_DrawsTheDotWithNothingToClick()
	{
		var cut = RenderToggle(isToggled: true, item: FeedItem(), readOnly: true);

		await Assert.That(cut.FindAll(".toggle-dot")).HasSingleItem();
		await Assert.That(cut.FindAll("button")).IsEmpty();
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

	private IRenderedComponent<Harness> RenderToggle(
		bool isToggled,
		WantlistItem? item,
		bool isToggling = false,
		bool readOnly = false,
		Action<MatchedMedia>? onToggle = null
	)
	{
		return Render<Harness>(parameters =>
			parameters
				.Add(harness => harness.IsToggled, isToggled)
				.Add(harness => harness.IsToggling, isToggling)
				.Add(harness => harness.ReadOnly, readOnly)
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
		public bool ReadOnly { get; set; }

		[Parameter]
		public WantlistItem? Item { get; set; }

		[Parameter]
		public EventCallback<MatchedMedia> OnToggle { get; set; }

		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<WantlistToggle>(1);
			builder.AddAttribute(2, nameof(WantlistToggle.Result), Movie());
			builder.AddAttribute(3, nameof(WantlistToggle.IsToggled), IsToggled);
			builder.AddAttribute(4, nameof(WantlistToggle.IsToggling), IsToggling);
			builder.AddAttribute(5, nameof(WantlistToggle.ReadOnly), ReadOnly);
			builder.AddAttribute(6, nameof(WantlistToggle.Item), Optional.Of(Item));
			builder.AddAttribute(7, nameof(WantlistToggle.OnToggle), OnToggle);
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
