using BlazorBlueprint.Components;
using Bunit;
using Contracts.Media;
using Domain.Media;
using Microsoft.Extensions.DependencyInjection;
using UI.Shared.Media;

namespace UI.Tests.Media;

public class BulkEditableMediaGridTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(RatingStub.Context());
		Services.AddLogging();
		Services.AddSingleton(IMediaService.Mock().Object);
		Services.AddSingleton(ArtworkStub.Sizing());
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task SelectionClick_InEditMode_FillsTheTilesEmptyBadgeWithACheckAndCountsItInTheBar()
	{
		var cut = Render<BulkEditableMediaGrid<MatchedMedia>>(parameters =>
			parameters
				.Add(grid => grid.Items, [Movie()])
				.Add(grid => grid.Detail, media => media)
				.Add(grid => grid.Key, media => media.Identity.Id)
				.Add(grid => grid.IsEditMode, true)
		);

		await Assert.That(cut.FindAll("[data-testid=selection-badge] svg")).IsEmpty();

		await cut.Find("[data-testid=selection-layer]").ClickAsync(new());

		await Assert.That(cut.FindAll("[data-testid=selection-badge] svg")).Count().IsEqualTo(1);
		await Assert.That(Collapsed(cut.Find(".bulk-sticky").TextContent)).Contains("1 selected");
	}

	private static MatchedMedia Movie()
	{
		return new(new MediaIdentity(MediaId.New(), []), new MovieDetails { BackdropUrl = "https://cdn.example.test/backdrop.jpg" });
	}

	private static string Collapsed(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
