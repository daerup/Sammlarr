using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Domain.Media;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Feeds.Pages.Editor;
using UI.Shared.Services.Branding;

namespace UI.Tests.Feeds;

public class FeedVotesPopoverTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton<MediaProviderBrandingService>();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Popover_WhenOpened_ListsAProviderPerRowAndTheRottenTomatoesLine()
	{
		var cut = Render<Harness>(parameters => parameters.Add(harness => harness.Strategy, new RatingStrategy(AveragedRating.Everything)));

		await cut.Find("button.field-segment").ClickAsync();

		var rows = cut.WaitForElements(".votes-pop-row");
		await Assert.That(rows.Select(row => row.TextContent.Trim())).IsEquivalentTo(["TMDB", "IMDb", "Rotten Tomatoesno votes"]);
		await Assert.That(cut.Find(".votes-pop").TextContent).Contains(VotesExplanation.RottenTomatoesLine);
	}

	[Test]
	public async Task Popover_WhenAverageCountsRottenTomatoesAlone_Warns()
	{
		var averaged = new AveragedRating([RatingSource.RottenTomatoesCritics]);
		var cut = Render<Harness>(parameters => parameters.Add(harness => harness.Strategy, new RatingStrategy(averaged)));

		await cut.Find("button.field-segment").ClickAsync();

		await Assert.That(cut.WaitForElement(".votes-pop-warning").TextContent).StartsWith("Every title Rotten Tomatoes rates fails this rule");
	}

	private sealed class Harness : ComponentBase
	{
		private static readonly Optional<RatingStrategy> FollowsDefault = new None();

		private static readonly ValueSet<RatingSource> Everything = [.. Enum.GetValues<RatingSource>()];

		[Parameter]
		public RatingStrategy Strategy { get; set; }

		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<FeedVotesPopover>(1);
			builder.AddComponentParameter(2, nameof(FeedVotesPopover.Value), FollowsDefault);
			builder.AddComponentParameter(3, nameof(FeedVotesPopover.Default), Strategy);
			builder.AddComponentParameter(4, nameof(FeedVotesPopover.Available), Everything);
			builder.CloseComponent();
		}
	}
}
