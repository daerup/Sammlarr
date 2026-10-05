using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Domain.Media;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
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
	public async Task Title_WithoutACard_ShowsNoTriggerAndOnlyTheTitle()
	{
		IReadOnlyList<IExternalMediaId> ids = [RottenTomatoesId.From("m/shawshank_redemption", MediaType.Movie), TvdbId.From("12", MediaType.Unknown)];

		var cut = Render<Harness>(parameters => parameters.Add(harness => harness.Ids, ids));

		await Assert.That(cut.FindAll(".media-title-trigger")).IsEmpty();
		await Assert.That(cut.Markup).Contains("The Shawshank Redemption");
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
