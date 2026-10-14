using BlazorBlueprint.Components;
using BlazorBlueprint.Icons.Lucide.Components;
using Bunit;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Wantlist.Components;

namespace UI.Tests.Wantlist;

public class ProvenanceDetailsTests : BunitContext
{
	private static readonly Guid Feed = new(21, 0, 0, new byte[8]);
	private static readonly Guid Run = new(22, 0, 0, new byte[8]);

	[Test]
	public async Task Render_ForAFeedRun_LinksTheNameToTheEditorAndTheFooterToTheRun()
	{
		var cut = RenderDetails(TestFeeds.Origin(Feed, Run, "snoak/latest"));

		var links = cut.FindAll("a").Select(link => link.GetAttribute("href")).ToList();

		await Assert.That(cut.Find(".provenance-name-text").TextContent.Trim()).IsEqualTo("snoak/latest");
		await Assert.That(links).HasCount(2);
		await Assert.That(links[0]).IsEqualTo($"/feeds/edit/{UrlGuid.Encode(Feed)}");
		await Assert.That(links[1]).IsEqualTo($"/feeds/{UrlGuid.Encode(Feed)}/runs/{UrlGuid.Encode(Run)}");
	}

	[Test]
	public async Task Render_ForARunThatMovedTitles_ShowsWhatItAddedAndRemoved()
	{
		var origin = TestFeeds.Origin(Feed, Run);
		var moved = origin with { Run = origin.Run with { Counts = origin.Run.Counts with { Added = 12, Removed = 3 } } };

		var cut = RenderDetails(moved);

		await Assert.That(cut.Find(".provenance-value .is-added").TextContent.Trim()).IsEqualTo("+12");
		await Assert.That(cut.Find(".provenance-value .is-removed").TextContent.Trim()).IsEqualTo("−3");
	}

	[Test]
	public async Task Render_ForARunThatMovedNothing_OmitsTheRunRowRatherThanShowingZero()
	{
		var cut = RenderDetails(TestFeeds.Origin(Feed, Run));

		await Assert.That(cut.Find(".provenance-meta").TextContent).DoesNotContain("Run");
	}

	[Test]
	public async Task Render_ForASinkItem_NamesTheSourceWithoutRepeatingTheProvider()
	{
		var cut = RenderDetails(new SinkOrigin(SinkKind.Plex));

		await Assert.That(cut.Find(".provenance-name-text").TextContent.Trim()).IsEqualTo("Media service");
		await Assert.That(cut.FindAll("a")).IsEmpty();
	}

	[Test]
	[Arguments(MediaType.Movie, "radarr")]
	[Arguments(MediaType.Show, "sonarr")]
	public async Task Render_ForATitleFoundOnRadarrOrSonarr_ShowsOnlyThatAppsMark(MediaType type, string app)
	{
		var cut = RenderDetails(new SinkOrigin(SinkKind.Arr), type);

		await Assert
			.That(cut.FindAll(".provenance-mark img").Select(mark => mark.GetAttribute("src") ?? string.Empty).Where(src => src.Contains("light")))
			.IsEquivalentTo([$"_content/UI/providers/{app}-square-light.svg"]);
	}

	[Test]
	public async Task Render_ForAnItemWhoseFeedWasDeleted_KeepsTheNameWearsTheGhostAndOffersNoDeadLinks()
	{
		var cut = RenderDetails(new OrphanedOrigin("Weekly picks"));

		var ghost = Render<LucideIcon>(parameters => parameters.Add(icon => icon.Name, "ghost").Add(icon => icon.Size, 14)).Find("path").GetAttribute("d");
		await Assert.That(cut.Find(".provenance-name-text").TextContent.Trim()).IsEqualTo("Weekly picks");
		await Assert.That(cut.Find(".provenance-note").TextContent.Trim()).IsEqualTo("This feed no longer exists");
		await Assert.That(cut.FindAll("svg path").Select(path => path.GetAttribute("d"))).Contains(ghost);
		await Assert.That(cut.FindAll("a")).IsEmpty();
	}

	private IRenderedComponent<ProvenanceDetails> RenderDetails(WantlistItemProvenance provenance, MediaType type = MediaType.Movie)
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddLogging();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;

		return Render<ProvenanceDetails>(parameters =>
			parameters.Add(details => details.Provenance, provenance).Add(details => details.AddedAt, DateTimeOffset.UnixEpoch).Add(details => details.Type, type)
		);
	}
}
