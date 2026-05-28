using System.Net;
using Contracts.Feed;
using Contracts.Media;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Feed;

[Binding]
public class FeedSteps(
	FeedScenarioContext ctx,
	MediaDetailContext detailCtx,
	SammlarrScenarioContext sammlarr,
	MediaCatalogContext catalog,
	CachedMediaContext cachedMedia,
	ExceptionContext exceptionContext
)
{
	[Given("the MDBList feed {string} as {string}")]
	public void GivenFeed(string identifier, string name) => ctx.Feeds[name] = new FeedRegistration { Identifier = identifier };

	[Given("{string} contains {string}")]
	public void GivenFeedContains(string feedName, string movieKey) => ctx.Feeds[feedName].Items.Add(catalog[movieKey]);

	[Given("{string} contains {string} with IMDb ID {string}")]
	public void GivenFeedContainsWithImdbId(string feedName, string movieKey, string imdbId)
	{
		var media = catalog[movieKey];
		ctx.Feeds[feedName].Items.Add(media);
		ctx.Feeds[feedName].ImdbIds[media.Title] = imdbId;
	}

	[When("I load the feed {string}")]
	public async Task WhenLoadFeed(string feedName)
	{
		var feed = ctx.Feeds[feedName];
		StubFeedUrl(feed);
		detailCtx.Items = await sammlarr.ResolveKeyed<IFeedService>(FeedProvider.Mdblist).GetFeedItemsAsync(feed.Identifier).ToListAsync();
	}

	[When("I try to load the feed {string}")]
	public async Task WhenTryLoadFeed(string feedName)
	{
		var feed = ctx.Feeds[feedName];
		StubFeedUrl(feed);
		await exceptionContext.TryExecuteAsync(async () =>
			detailCtx.Items = await sammlarr.ResolveKeyed<IFeedService>(FeedProvider.Mdblist).GetFeedItemsAsync(feed.Identifier).ToListAsync()
		);
	}

	[When("I reload the feed {string} without provider stubs")]
	public async Task WhenReloadFeedWithoutProviderStubs(string feedName)
	{
		var feed = ctx.Feeds[feedName];
		sammlarr.HttpHandler.ClearUrlStubs();
		sammlarr.HttpHandler.RespondWith(HttpStatusCode.InternalServerError);
		StubFeedUrl(feed);
		detailCtx.Items = await sammlarr.ResolveKeyed<IFeedService>(FeedProvider.Mdblist).GetFeedItemsAsync(feed.Identifier).ToListAsync();
	}

	[Then("{string} has the external ID {string} stored")]
	public async Task ThenHasExternalIdStored(string key, string imdbId)
	{
		sammlarr.DbContext.ChangeTracker.Clear();
		var entityId = cachedMedia.MediaIds[key].Value;
		var hasImdb = await sammlarr.DbContext.ExternalMediaIds.AnyAsync(e => e.Media.Id == entityId && e.Source == MediaProvider.Imdb && e.Id == imdbId);
		await Assert.That(hasImdb).IsTrue();
	}

	private void StubFeedUrl(FeedRegistration feed) =>
		sammlarr.HttpHandler.RespondWithJsonForUrl($"lists/{feed.Identifier}/items", BuildFeedJson(feed.Items, feed.ImdbIds));

	private static string BuildFeedJson(IReadOnlyList<RegisteredMedia> items, Dictionary<string, string> imdbIds)
	{
		var movies = string.Join(
			",",
			items.Select(
				(item, i) =>
				{
					var imdbId = imdbIds.TryGetValue(item.Title, out var id) ? $"\"{id}\"" : "null";
					return $$"""
					{
					  "id": {{i + 1}},
					  "ids": {"imdb": {{imdbId}}, "tmdb": {{item.TmdbId}}, "tvdb": null, "mdblist": null, "mal": null},
					  "rank": {{i + 1}},
					  "title": "{{item.Title}}",
					  "status": "Released",
					  "country": "US",
					  "runtime": 139,
					  "mediatype": "movie",
					  "release_date": "1999-10-15",
					  "release_year": 1999,
					  "spoken_language": "English"
					}
					""";
				}
			)
		);
		return $$"""
			{
			  "movies": [{{movies}}],
			  "shows": []
			}
			""";
	}
}
