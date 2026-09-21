using BlazorBlueprint.Components;
using Bunit;
using Domain.Watchlist;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Watchlist.Components;
using UI.Shared.Services.Branding;

namespace UI.Tests.Watchlist;

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

	private IRenderedComponent<ProvenanceBadge> RenderBadge(WatchlistItemProvenance provenance)
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddLogging();
		Services.AddScoped(_ => new MediaProviderBrandingService(string.Empty));
		JSInterop.Mode = JSRuntimeMode.Loose;

		return Render<ProvenanceBadge>(parameters => parameters.Add(badge => badge.Provenance, provenance).Add(badge => badge.AddedAt, DateTimeOffset.UtcNow));
	}
}
