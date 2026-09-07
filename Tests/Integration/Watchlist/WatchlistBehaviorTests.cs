using Contracts.Watchlist;
using DB;
using DB.Data.Watchlist;
using Domain.Media;
using Domain.Providers;
using Domain.Watchlist;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Watchlist;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class WatchlistBehaviorTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly WatchlistItemProvenance Manual = new(new ManualOrigin(ManualSource.Unknown));
	private static readonly WatchlistItemProvenance Remote = new(new RemoteOrigin(WatchlistProvider.Plex));

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Adding_media_with_manual_origin_persists_the_entry()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();

		await host.Resolve<IWatchlistService>().AddToWatchlistAsync([inception], Manual);

		var entries = await host.ScopedDbContext.Watchlist.AsNoTracking().ToListAsync();
		await Assert.That(entries).Count().IsEqualTo(1);
		await Assert.That(entries[0].Origin).IsEqualTo(WatchlistEntryOrigin.Manual);
	}

	[Test]
	public async Task Adding_an_entry_that_is_already_present_keeps_its_original_origin()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWatchlist(origin: Remote);

		await host.Resolve<IWatchlistService>().AddToWatchlistAsync([inception], Manual);

		var entries = await host.ScopedDbContext.Watchlist.AsNoTracking().ToListAsync();
		await Assert.That(entries).Count().IsEqualTo(1);
		await Assert.That(entries[0].Origin).IsEqualTo(WatchlistEntryOrigin.Remote);
	}

	[Test]
	public async Task Removing_an_entry_hard_deletes_it()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWatchlist(origin: Manual);

		await host.Resolve<IWatchlistService>().RemoveFromWatchlistAsync([inception]);

		await Assert.That(await host.ScopedDbContext.Watchlist.AsNoTracking().CountAsync()).IsEqualTo(0);
	}

	[Test]
	public async Task Getting_watchlist_ids_returns_ids_of_all_watchlisted_items()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).OnWatchlist(origin: Remote);
		var inception = await host.Movie(TestMedia.Inception).OnWatchlist(origin: Remote);

		var ids = await host.Resolve<IWatchlistService>().GetWatchlistIdsAsync();

		await Assert.That(ids).Contains(fightClub);
		await Assert.That(ids).Contains(inception);
	}

	[Test]
	public async Task Adding_media_without_the_watchlist_providers_id_is_rejected()
	{
		var solaris = await host.Movie("Solaris", tmdb: "11321").SeedAsync();

		await Assert.That(async () => await host.Resolve<IWatchlistService>().AddToWatchlistAsync([solaris], Manual)).Throws<MissingWatchlistExternalIdException>();
	}

	[Test]
	public async Task Removing_media_without_the_watchlist_providers_id_is_rejected()
	{
		var solaris = await host.Movie("Solaris", tmdb: "11321").OnWatchlist(origin: Manual);

		await Assert.That(async () => await host.Resolve<IWatchlistService>().RemoveFromWatchlistAsync([solaris])).Throws<MissingWatchlistExternalIdException>();
	}

	[Test]
	public async Task Getting_watchlist_items_enriches_entries_with_metadata_and_rating()
	{
		await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb(rating: 8.4, votes: 23000).OnWatchlist(origin: Remote);

		var items = await host.Resolve<IWatchlistService>().GetWatchlistItemsAsync(WatchlistQueryOptions.Default).ToListAsync();

		await Assert.That(items).Count().IsEqualTo(1);
		await Assert
			.That(items[0].Media)
			.IsOfUnionType<IdentifiedMedia, PartialMovie>()
			.And.Multiple(async movie =>
			{
				await Assert.That(movie.Title).IsEqualTo("Fight Club");
				await Assert.That(movie.PosterUrl).IsNotNullOrEmpty();
				await Assert.That(movie.Rating.Sources.Any(source => source.Source == RatingProvider.Tmdb)).IsTrue();
			});
	}
}
