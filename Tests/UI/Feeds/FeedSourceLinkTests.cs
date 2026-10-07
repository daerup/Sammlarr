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
	[Arguments(FeedProvider.Mdblist, "snoak/trakt-s-trending-shows", "https://mdblist.com/lists/snoak/trakt-s-trending-shows")]
	[Arguments(FeedProvider.Mdblist, "chazlarson/external/2868", "https://mdblist.com/lists/chazlarson/external/2868")]
	[Arguments(FeedProvider.Tmdb, "8231234", "https://www.themoviedb.org/list/8231234")]
	[Arguments(FeedProvider.Tmdb, "movie/top-rated", "https://www.themoviedb.org/movie/top-rated")]
	[Arguments(FeedProvider.Tmdb, "discover/tv?with_genres=16", "https://www.themoviedb.org/discover/tv?with_genres=16")]
	[Arguments(FeedProvider.AniList, "yourname/planning", "https://anilist.co/user/yourname/animelist")]
	[Arguments(FeedProvider.AniList, "search/anime?airing%20status=RELEASING", "https://anilist.co/search/anime?airing%20status=RELEASING")]
	[Arguments(FeedProvider.AniList, "search/anime/this-season", "https://anilist.co/search/anime/this-season")]
	[Arguments(FeedProvider.Mal, "yourname", "https://myanimelist.net/animelist/yourname")]
	[Arguments(FeedProvider.Mal, "topanime.php?type=upcoming", "https://myanimelist.net/topanime.php?type=upcoming")]
	[Arguments(FeedProvider.Mal, "anime/season/2026/fall", "https://myanimelist.net/anime/season/2026/fall")]
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
