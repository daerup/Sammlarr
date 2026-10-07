using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Domain.Media;
using Microsoft.AspNetCore.Components;
using UI.Shared.Media.Ratings;

namespace UI.Tests.Media.Ratings;

public class RatingSuppliersPopoverTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Popover_ForASourceOnlyPlexSupplies_OffersToConnectPlex()
	{
		var cut = await OpenAsync(RatingSource.Imdb);

		var supplier = cut.Find("a.rating-supplier");
		await Assert.That(cut.Find(".rating-suppliers-head").TextContent).IsEqualTo("IMDb ratings come from");
		await Assert.That(supplier.GetAttribute("href")).IsEqualTo("/settings?section=providers&provider=plex");
		await Assert.That(supplier.TextContent).Contains("Plex").And.Contains("Not connected");
	}

	[Test]
	public async Task Popover_ForSeveralSources_ListsEachSupplierOnce()
	{
		var cut = await OpenAsync(RatingSource.Tmdb, RatingSource.Imdb);

		await Assert
			.That(cut.FindAll("a.rating-supplier").Select(Href))
			.IsEquivalentTo([
				"/settings?section=providers&provider=tmdb",
				"/settings?section=providers&provider=plex",
				"/settings?section=providers&provider=mdblist",
			]);
		await Assert
			.That(cut.FindAll("a.rating-supplier").Single(link => Href(link).EndsWith("=tmdb", StringComparison.Ordinal)).TextContent)
			.Contains("No key yet");
	}

	[Test]
	public async Task Popover_ForImdb_LeavesOutItsDatasetsWhichNeedNoConnecting()
	{
		var cut = await OpenAsync(RatingSource.Imdb);

		await Assert
			.That(cut.FindAll("a.rating-supplier").Select(Href))
			.IsEquivalentTo(["/settings?section=providers&provider=plex", "/settings?section=providers&provider=mdblist"]);
	}

	private static string Href(IElement link) => link.GetAttribute("href") ?? string.Empty;

	private async Task<IRenderedComponent<IComponent>> OpenAsync(params RatingSource[] sources)
	{
		var cut = Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<RatingSuppliersPopover>(1);
			builder.AddComponentParameter(2, nameof(RatingSuppliersPopover.Sources), sources);
			builder.CloseComponent();
		});
		await cut.Find(".rating-suppliers-link").ClickAsync(new());
		return cut;
	}
}
