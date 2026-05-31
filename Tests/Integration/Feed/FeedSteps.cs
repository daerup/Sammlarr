using API.Mdblist;
using Contracts.Feed;
using Contracts.Media;
using Integration.Tests.Assertions;
using Integration.Tests.Infrastructure.Common;
using Integration.Tests.Infrastructure.Hosting;
using Integration.Tests.Infrastructure.Media;
using Integration.Tests.Infrastructure.Media.Local;
using Microsoft.EntityFrameworkCore;
using Reqnroll.Assist;

namespace Integration.Tests.Feed;

[Binding]
public class FeedSteps(FeedScenarioContext ctx, SammlarrScenarioContext sammlarr, ExceptionContext exceptionContext, LocalMediaContext cachedMedia)
{
	[Given("the MDBList feed {string} as {string} contains:")]
	public void GivenFeedContents(string identifier, string feedName, DataTable table)
	{
		var registration = new FeedRegistration { Identifier = identifier };
		ctx.Feeds[feedName] = registration;
		foreach (var row in table.CreateSet<MediaRow>())
			registration.Items.Add(ParseRow(row));
		StubFeedUrl(registration);
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

	private static FeedItemSpec ParseRow(MediaRow row)
	{
		var type = string.IsNullOrEmpty(row.Type) ? MediaType.Movie : StepTransformers.ParseMediaKind(row.Type);
		var title = Cells.IsNull(row.Title) || string.IsNullOrEmpty(row.Title) ? null : row.Title;
		var ids = row.Ids ?? new Dictionary<MediaProvider, string>();

		return new FeedItemSpec(title, type, row.Year, ids);
	}
}
