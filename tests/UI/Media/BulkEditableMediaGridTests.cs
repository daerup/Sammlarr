using BlazorBlueprint.Components;
using Bunit;
using Contracts.Media;
using Domain.Media;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using UI.Shared.Layout;
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
		await Assert.That(cut.Find(".bulk-sticky").TextContent.Words()).Contains("1 selected");
	}

	[Test]
	[Arguments(true, 0)]
	[Arguments(false, 1)]
	public async Task Overlay_InEditMode_HidesOnAPhoneOnly(bool phone, int overlays)
	{
		RenderTree.TryAdd<CascadingValue<bool>>(cascade => cascade.Add(value => value.Name, PhoneLayout.IsPhoneCascade).Add(value => value.Value, phone));

		var cut = Render<BulkEditableMediaGrid<MatchedMedia>>(parameters =>
			parameters
				.Add(grid => grid.Items, [Movie()])
				.Add(grid => grid.Detail, media => media)
				.Add(grid => grid.Key, media => media.Identity.Id)
				.Add(grid => grid.IsEditMode, true)
				.Add(grid => grid.Overlay, _ => builder => builder.AddMarkupContent(0, "<span class=\"badge\">Manual</span>"))
		);

		await Assert.That(cut.FindAll(".badge")).Count().IsEqualTo(overlays);
	}

	private static MatchedMedia Movie()
	{
		return new(new MediaIdentity(MediaId.New(), []), new MovieDetails { BackdropUrl = "https://cdn.example.test/backdrop.jpg" });
	}
}
