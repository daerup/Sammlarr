using Contracts.Media;
using Contracts.Watchlist;
using DB.Data.Media;
using DB.Data.Watchlist;
using Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Watchlist;

[Binding]
public class WatchlistServiceSteps(WatchlistServiceScenarioContext ctx, SammlarrScenarioContext sammlarr)
{
	[Given("a media item exists with Plex id {string}")]
	public async Task GivenMediaExists(string plexId) => ctx.CurrentMediaId = await SeedMediaAsync(PlexId.From(plexId, MediaType.Movie));

	[Given("a media item is already on the watchlist with {word} origin and Plex id {string}")]
	public async Task GivenWatchlistEntry(string origin, string plexId) =>
		ctx.CurrentMediaId = await SeedWatchlistEntryAsync(PlexId.From(plexId, MediaType.Movie), ParseOrigin(origin));

	[When("I add it to the watchlist with {word} origin")]
	public async Task WhenAdd(string origin) =>
		await sammlarr.Resolve<IWatchlistRepository>().AddAsync([ctx.CurrentMediaId], ToProvenance(ParseOrigin(origin)));

	[When("I remove it from the watchlist")]
	public async Task WhenRemove() => await sammlarr.Resolve<IWatchlistRepository>().RemoveAsync([ctx.CurrentMediaId]);

	[Then("the watchlist contains {int} entry with {word} origin")]
	public async Task ThenContainsEntry(int count, string origin)
	{
		var entries = await sammlarr.DbContext.Watchlist.AsNoTracking().ToListAsync();
		await Assert.That(entries).Count().IsEqualTo(count);
		await Assert.That(entries[0].Origin).IsEqualTo(ParseOrigin(origin));
	}

	[Then("the watchlist is empty")]
	public async Task ThenWatchlistIsEmpty()
	{
		var count = await sammlarr.DbContext.Watchlist.CountAsync();
		await Assert.That(count).IsEqualTo(0);
	}

	private async Task<MediaId> SeedMediaAsync(IExternalMediaId id)
	{
		var media = new MediaEntity();
		await sammlarr.DbContext.ExternalMediaIds.AddAsync(id.ToExternalIdEntity(media));
		await sammlarr.DbContext.SaveChangesAsync();
		sammlarr.DbContext.ChangeTracker.Clear();
		return MediaId.From(media.Id);
	}

	private async Task<MediaId> SeedWatchlistEntryAsync(IExternalMediaId id, WatchlistEntryOrigin origin)
	{
		var media = new MediaEntity();
		await sammlarr.DbContext.ExternalMediaIds.AddAsync(id.ToExternalIdEntity(media));
		sammlarr.DbContext.Watchlist.Add(new WatchlistEntity { Media = media, Origin = origin });
		await sammlarr.DbContext.SaveChangesAsync();
		sammlarr.DbContext.ChangeTracker.Clear();
		return MediaId.From(media.Id);
	}

	private static WatchlistEntryOrigin ParseOrigin(string origin) => Enum.Parse<WatchlistEntryOrigin>(origin, ignoreCase: true);

	private static WatchlistItemProvenance ToProvenance(WatchlistEntryOrigin origin) =>
		origin switch
		{
			WatchlistEntryOrigin.Manual => new WatchlistItemProvenance(new ManualOrigin()),
			WatchlistEntryOrigin.Remote => new WatchlistItemProvenance(new RemoteOrigin()),
			_ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null),
		};
}
