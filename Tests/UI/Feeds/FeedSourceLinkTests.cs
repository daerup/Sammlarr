using BlazorBlueprint.Components;
using Bunit;
using Domain.Providers;
using UI.Features.Feeds.Components;

namespace UI.Tests.Feeds;

public class FeedSourceLinkTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	[Arguments(FeedProvider.Mdblist, "top-250", "https://mdblist.com/lists/top-250")]
	[Arguments(FeedProvider.Tmdb, "12345", "https://www.themoviedb.org/list/12345")]
	public async Task Render_ForAProvider_LinksToItsListUrl(FeedProvider provider, string identifier, string expectedUrl)
	{
		var cut = Render<FeedSourceLink>(parameters => parameters.Add(link => link.Provider, provider).Add(link => link.Identifier, identifier));

		var anchor = cut.Find("a");
		await Assert.That(anchor.GetAttribute("href")).IsEqualTo(expectedUrl);
		await Assert.That(anchor.TextContent).Contains(identifier);
	}

	[Test]
	public async Task Render_ForAnExternalLink_OpensSafelyInANewTabAsAnAppLink()
	{
		var cut = Render<FeedSourceLink>(parameters => parameters.Add(link => link.Provider, FeedProvider.Mdblist).Add(link => link.Identifier, "some-list"));

		var anchor = cut.Find("a");
		await Assert.That(anchor.ClassList).Contains("app-link");
		await Assert.That(anchor.GetAttribute("target")).IsEqualTo("_blank");
		await Assert.That(anchor.GetAttribute("rel")).IsEqualTo("noopener noreferrer");
	}

	[Test]
	public async Task Render_ForAProvider_TitlesTheLinkWithWhereItOpens()
	{
		var cut = Render<FeedSourceLink>(parameters => parameters.Add(link => link.Provider, FeedProvider.Mdblist).Add(link => link.Identifier, "some-list"));

		await Assert.That(cut.Find("a").GetAttribute("title")).IsEqualTo("Open on MDBList");
	}

	[Test]
	public async Task Render_WhenMuted_MutesTheLink()
	{
		var cut = Render<FeedSourceLink>(parameters =>
			parameters.Add(link => link.Provider, FeedProvider.Mdblist).Add(link => link.Identifier, "some-list").Add(link => link.Muted, true)
		);

		await Assert.That(cut.Find("a").ClassList).Contains("is-muted");
	}

	[Test]
	public async Task Render_WithASizeClass_ReplacesTheDefaultSize()
	{
		var cut = Render<FeedSourceLink>(parameters =>
			parameters.Add(link => link.Provider, FeedProvider.Mdblist).Add(link => link.Identifier, "some-list").Add(link => link.Class, "text-[11px]")
		);

		var classes = cut.Find("a").ClassList;
		await Assert.That(classes).Contains("text-[11px]");
		await Assert.That(classes).DoesNotContain("text-sm");
	}
}
