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

public class ProvenanceBadgeTests : BunitContext
{
	private static readonly Guid Feed = new(21, 0, 0, new byte[8]);
	private static readonly Guid Run = new(22, 0, 0, new byte[8]);

	[Test]
	public async Task Render_ForATitleAddedByARun_LinksToThatRunRatherThanTheFeedEditor()
	{
		var cut = RenderBadge(TestFeeds.Origin(Feed, Run));

		var href = cut.Find("a").GetAttribute("href");

		await Assert.That(href).IsEqualTo($"/feeds/{UrlGuid.Encode(Feed)}/runs/{UrlGuid.Encode(Run)}");
	}

	[Test]
	public async Task Render_WhenTheCallerKnowsTheTitle_PointsTheRunAtItSoItCanBeScrolledTo()
	{
		var item = MediaId.From(new Guid(23, 0, 0, new byte[8]));

		var cut = RenderBadge(TestFeeds.Origin(Feed, Run), item);

		var href = cut.Find("a").GetAttribute("href");

		await Assert.That(href).IsEqualTo($"/feeds/{UrlGuid.Encode(Feed)}/runs/{UrlGuid.Encode(Run)}?item={UrlGuid.Encode(item.Value)}");
	}

	[Test]
	public async Task Render_ForATitleWhoseFeedIsGone_IsNotALinkAtAll()
	{
		var cut = RenderBadge(new OrphanedOrigin("Old feed"));

		await Assert.That(cut.FindAll("a")).IsEmpty();
	}

	[Test]
	public async Task Render_ForATitleAddedByHand_IsNotALinkAtAll()
	{
		var cut = RenderBadge(new ManualOrigin(ManualSource.Search));

		await Assert.That(cut.FindAll("a")).IsEmpty();
	}

	[Test]
	[Arguments(MediaType.Movie, "Radarr")]
	[Arguments(MediaType.Show, "Sonarr")]
	public async Task Render_ForATitleAddedOnRadarrOrSonarr_NamesTheAppItCameFrom(MediaType type, string app)
	{
		var cut = RenderBadge(new SinkOrigin(SinkKind.Arr), type: type);

		await Assert.That(cut.Find(".wl-badge-label").TextContent.Trim()).IsEqualTo(app);
	}

	[Test]
	public async Task Render_ForATitleWhoseFeedIsGone_WearsTheGhost()
	{
		var cut = RenderBadge(new OrphanedOrigin("Old feed"));

		var ghost = Render<LucideIcon>(parameters => parameters.Add(icon => icon.Name, "ghost").Add(icon => icon.Size, 14)).Find("path").GetAttribute("d");
		await Assert.That(cut.FindAll("svg path").Select(path => path.GetAttribute("d"))).Contains(ghost);
	}

	private IRenderedComponent<ProvenanceBadge> RenderBadge(WantlistItemProvenance provenance, MediaId? itemId = null, MediaType type = MediaType.Movie)
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddLogging();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;

		return Render<ProvenanceBadge>(parameters =>
			parameters
				.Add(badge => badge.Provenance, provenance)
				.Add(badge => badge.AddedAt, DateTimeOffset.UtcNow)
				.Add(badge => badge.ItemId, itemId)
				.Add(badge => badge.Type, type)
		);
	}
}
