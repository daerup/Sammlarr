using System.Net;
using System.Text;
using System.Web;
using API.Mdblist;
using Contracts.Feed;
using Contracts.Media;
using Contracts.Media.Search;
using Domain.Media;
using Domain.Providers;
using Integration.Tests.Assertions;
using Integration.Tests.Infrastructure.Common;
using Integration.Tests.Infrastructure.Hosting;
using Integration.Tests.Infrastructure.Media;
using Integration.Tests.Infrastructure.Media.Local;
using Microsoft.EntityFrameworkCore;
using Reqnroll;
using Reqnroll.Assist;

namespace Integration.Tests.Feeds;

[Binding]
public class FeedSteps(FeedScenarioContext ctx, SammlarrScenarioContext sammlarr, ExceptionContext exceptionContext, LocalMediaContext cachedMedia)
{
	[Given("the MDBList feed {string} as {string} contains:")]
	public void GivenFeedContents(string identifier, string feedName, DataTable table)
	{
		var registration = new FeedRegistration { Identifier = identifier };
		ctx.Feeds[feedName] = registration;
		foreach (var row in table.CreateSet<FeedItemRow>())
			registration.Items.Add(ParseRow(row));
		StubFeedUrl(registration);
	}

	[Given("the MDBList feed {string} as {string} returns HTTP {int}")]
	public void GivenFeedReturnsStatus(string identifier, string feedName, int statusCode)
	{
		ctx.Feeds[feedName] = new FeedRegistration { Identifier = identifier };
		sammlarr.HttpHandler.RespondWithStatusForUrl(string.Format(MdblistEndpoints.ListItemsPath, identifier), (HttpStatusCode)statusCode);
	}

	[Given("the MDBList feed {string} as {string} returns invalid JSON")]
	public void GivenFeedReturnsInvalidJson(string identifier, string feedName)
	{
		ctx.Feeds[feedName] = new FeedRegistration { Identifier = identifier };
		sammlarr.HttpHandler.RespondForUrl(
			string.Format(MdblistEndpoints.ListItemsPath, identifier),
			_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>not json</html>", Encoding.UTF8, "application/json") }
		);
	}

	[Given("the MDBList feed {string} as {string} is empty")]
	public void GivenFeedEmpty(string identifier, string feedName)
	{
		ctx.Feeds[feedName] = new FeedRegistration { Identifier = identifier };
		sammlarr.HttpHandler.RespondWithJsonForUrl(string.Format(MdblistEndpoints.ListItemsPath, identifier), """{ "movies": [], "shows": [] }""");
	}

	[Given("the MDBList feed {string} as {string} is paginated:")]
	public void GivenFeedPaginated(string identifier, string feedName, DataTable table)
	{
		ctx.Feeds[feedName] = new FeedRegistration { Identifier = identifier };
		var pages = table.CreateSet<PaginatedPageRow>().ToDictionary(page => page.Offset);
		sammlarr.HttpHandler.RespondForUrl(
			string.Format(MdblistEndpoints.ListItemsPath, identifier),
			request =>
			{
				var offset = int.Parse(HttpUtility.ParseQueryString(request.RequestUri?.Query ?? string.Empty)["offset"] ?? "0");
				var page = pages[offset];
				var items = (page.Titles ?? []).Select(title => new FeedItemSpec(title, MediaType.Movie, null, new Dictionary<MediaProvider, string>())).ToList();
				var response = new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(MdblistFeedJson.Build(items), Encoding.UTF8, "application/json"),
				};
				if (page.HasMore)
					response.Headers.TryAddWithoutValidation("X-Has-More", "true");
				return response;
			}
		);
	}

	[When("I load the feed {string}")]
	public async Task WhenLoadFeed(string feedName) =>
		ctx.Items = await sammlarr.ResolveKeyed<IFeedService>(FeedProvider.Mdblist).GetFeedItemsAsync(ctx.Feeds[feedName].Identifier).ToListAsync();

	[When("I try to load the feed {string}")]
	public async Task WhenTryLoadFeed(string feedName) =>
		await exceptionContext.TryExecuteAsync(async () =>
			ctx.Items = await sammlarr.ResolveKeyed<IFeedService>(FeedProvider.Mdblist).GetFeedItemsAsync(ctx.Feeds[feedName].Identifier).ToListAsync()
		);

	[Then("the feed items are:")]
	public Task ThenItemsAre(DataTable table) => MediaDetailAssertions.AssertItemsAsync(ctx.Items!, table);

	[Then("the item count is {int}")]
	public async Task ThenItemCount(int count) => await Assert.That(ctx.Items!).Count().IsEqualTo(count);

	[Then("all feed items are unresolved")]
	public async Task ThenAllUnresolved() => await Assert.That(ctx.Items!.All(item => !(item is IdentifiedMedia identified && identified is IMedia))).IsTrue();

	[Then("{string} has the {MediaProvider} ID {string} stored")]
	public async Task ThenHasExternalIdStored(string key, MediaProvider provider, string id)
	{
		sammlarr.DbContext.ChangeTracker.Clear();
		var entityId = cachedMedia.MediaIds[key].Value;
		var exists = await sammlarr.DbContext.ExternalMediaIds.AnyAsync(e => e.Media.Id == entityId && e.Source == provider && e.Id == id);
		await Assert.That(exists).IsTrue();
	}

	private void StubFeedUrl(FeedRegistration feed) =>
		sammlarr.HttpHandler.RespondWithJsonForUrl(string.Format(MdblistEndpoints.ListItemsPath, feed.Identifier), MdblistFeedJson.Build(feed.Items));

	private static FeedItemSpec ParseRow(FeedItemRow row)
	{
		var type = string.IsNullOrEmpty(row.Type) ? MediaType.Movie : StepTransformers.ParseMediaKind(row.Type);
		var title = Cells.IsNull(row.Title) || string.IsNullOrEmpty(row.Title) ? null : row.Title;
		var ids = row.Ids ?? new Dictionary<MediaProvider, string>();

		return new FeedItemSpec(title, type, row.Year, ids);
	}

	private sealed class FeedItemRow
	{
		public IReadOnlyDictionary<MediaProvider, string>? Ids { get; set; }
		public string? Title { get; set; }
		public string? Type { get; set; }
		public int? Year { get; set; }
	}

	private sealed class PaginatedPageRow
	{
		public int Offset { get; set; }
		public IReadOnlyList<string>? Titles { get; set; }
		public bool HasMore { get; set; }
	}
}
