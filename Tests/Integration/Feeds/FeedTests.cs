using System.Text.Json;
using Contracts.Feed;
using Domain.Media;
using Domain.Providers;
using Harness;
using Integration.Tests.Assertions;
using Microsoft.EntityFrameworkCore;
using static Harness.Ids;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class FeedTests(SammlarrTestHost host) : IAsyncDisposable
{
	private IFeedProvider Testee => host.ResolveKeyed<IFeedProvider>(FeedProvider.Mdblist);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task GetFeedItemsAsync_WithoutAnApiKey_Throws()
	{
		host.MdblistFeedContains("top-250", Item("Fight Club", MediaType.Movie, Tmdb("550")));

		await Assert.That(async () => await Testee.GetFeedItemsAsync("top-250").ToListAsync()).Throws<Exception>().WithMessageContaining("No MDBList API key");
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenTheProviderReturnsAServerError_SurfacesTheFailure()
	{
		await host.WithMdblistAccount("test-key");
		host.MdblistFeedReturnsStatus("broken-list", System.Net.HttpStatusCode.InternalServerError);

		await Assert.That(async () => await Testee.GetFeedItemsAsync("broken-list").ToListAsync()).Throws<Exception>().WithMessageContaining("500");
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenTheBodyIsMalformed_FailsToLoad()
	{
		await host.WithMdblistAccount("test-key");
		host.MdblistFeedReturnsInvalidJson("corrupt-list");

		await Assert.That(async () => await Testee.GetFeedItemsAsync("corrupt-list").ToListAsync()).Throws<JsonException>();
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenTheProviderCircuitIsOpen_SurfacesATypedFailure()
	{
		await host.WithMdblistAccount("test-key");
		host.MdblistFeedUnavailable("storm-list");

		await Assert.That(async () => await Testee.GetFeedItemsAsync("storm-list").ToListAsync()).Throws<ProviderUnavailableException>();
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenTheProviderFailsMidLoad_StillYieldsTheItemsFetchedBefore()
	{
		await host.WithMdblistAccount("test-key");
		host.MdblistFeedPartialThenUnavailable("partial-list", Item("Item1", MediaType.Movie), Item("Item2", MediaType.Movie));

		var items = new List<MediaLookup>();
		await Assert
			.That(async () =>
			{
				await foreach (var item in Testee.GetFeedItemsAsync("partial-list"))
				{
					items.Add(item);
				}
			})
			.Throws<ProviderUnavailableException>();
		await Assert.That(items).Count().IsEqualTo(2);
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenTheFeedIsEmpty_LoadsNoItems()
	{
		await host.WithMdblistAccount("test-key");
		host.MdblistFeedEmpty("empty-list");

		await Assert.That(await Testee.GetFeedItemsAsync("empty-list").ToListAsync()).IsEmpty();
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenTheFeedIsPaged_LoadsEveryPage()
	{
		await host.WithMdblistAccount("test-key");
		host.MdblistFeedPaginated("big-list", Page(0, hasMore: true, "Item1", "Item2"), Page(1000, hasMore: false, "Item3"));

		await Assert.That(await Testee.GetFeedItemsAsync("big-list").ToListAsync()).Count().IsEqualTo(3);
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenEntriesAreDuplicated_PreservesThemInOrder()
	{
		await host.WithMdblistAccount("test-key");
		await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb().SeedAsync();
		host.MdblistFeedContains("dupes-list", Item("Fight Club", MediaType.Movie, Tmdb("550")), Item("Fight Club", MediaType.Movie, Tmdb("550")));

		await Assert.That(await Testee.GetFeedItemsAsync("dupes-list").ToListAsync()).Count().IsEqualTo(2);
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenEveryItemFailsStrictValidation_ReturnsThemAsUnmatched()
	{
		await host.WithMdblistAccount("test-key");
		host.MdblistFeedContains("loose-list", Item("No Ids One", MediaType.Movie), Item("No Ids Two", MediaType.Movie));

		var items = await Testee.GetFeedItemsAsync("loose-list").ToListAsync();

		await Assert.That(items).Count().IsEqualTo(2);
		await AssertAllUnmatched(items);
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenMetadataCannotBeResolved_ReturnsTheItemAsUnmatched()
	{
		await host.WithMdblistAccount("test-key");
		await host.WithPlexAccount("plex-token");
		await host.Movie(TestMedia.FightClub).SeedAsync();
		host.PlexLookupFails(TestMedia.FightClub);
		host.MdblistFeedContains("failing-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));

		var items = await Testee.GetFeedItemsAsync("failing-list").ToListAsync();

		await Assert.That(items).Count().IsEqualTo(1);
		await AssertAllUnmatched(items);
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenAnItemHasNoKnownIds_StillReturnsIt()
	{
		await host.WithMdblistAccount("test-key");
		host.MdblistFeedContains("mystery-list", Item("Unknown Movie", MediaType.Movie));

		var items = await Testee.GetFeedItemsAsync("mystery-list").ToListAsync();

		await AssertItems(items, ("Unknown Movie", Poster: false, Rating: null));
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenAnItemHasNoReleaseYear_ToleratesIt()
	{
		await host.WithMdblistAccount("test-key");
		host.MdblistFeedContains("no-year-list", Item("Orphaned", MediaType.Movie));

		var items = await Testee.GetFeedItemsAsync("no-year-list").ToListAsync();

		await AssertItems(items, ("Orphaned", Poster: false, Rating: null));
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenAnItemHasANullTitle_ToleratesIt()
	{
		await host.WithMdblistAccount("test-key");
		host.MdblistFeedContains("null-title-list", Item(null, MediaType.Movie, Tmdb("4242")));

		await Assert.That(await Testee.GetFeedItemsAsync("null-title-list").ToListAsync()).Count().IsEqualTo(1);
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenAnItemIsKnownOnlyByImdbId_StillReturnsIt()
	{
		await host.WithMdblistAccount("test-key");
		host.PlexHolds(Spec("Inception", Tmdb("27205"), Imdb("tt1375666"), Plex("27205")));
		await host.PlexSearchReturns("Inception", Spec("Inception", Plex("27205")));
		host.MdblistFeedContains("imdb-only-list", Item("Inception", MediaType.Movie, Imdb("tt1375666")));

		var items = await Testee.GetFeedItemsAsync("imdb-only-list").ToListAsync();

		await AssertItems(items, ("Inception", Poster: true, Rating: null));
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenAnItemLoads_EnrichesItWithMetadataAndRating()
	{
		await host.WithMdblistAccount("test-key");
		await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb(rating: 8.4, votes: 23000).SeedAsync();
		host.MdblistFeedContains("top-250", Item("Fight Club", MediaType.Movie, Tmdb("550")));

		var items = await Testee.GetFeedItemsAsync("top-250").ToListAsync();

		await AssertItems(items, ("Fight Club", Poster: true, MediaProvider.Tmdb));
	}

	[Test]
	public async Task GetFeedItemsAsync_WithMultipleMovies_ResolvesEveryItem()
	{
		await host.WithMdblistAccount("test-key");
		await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb(rating: 8.4, votes: 23000).SeedAsync();
		await host.Movie(Spec("Inception", Tmdb("27205"), Imdb("tt1375666"), Plex("27205"))).ServedByPlex().ServedByTmdb(rating: 7.8, votes: 15000).SeedAsync();
		host.MdblistFeedContains("top-250", Item("Fight Club", MediaType.Movie, Tmdb("550")), Item("Inception", MediaType.Movie, Tmdb("27205")));

		var items = await Testee.GetFeedItemsAsync("top-250").ToListAsync();

		await AssertItems(items, ("Fight Club", Poster: false, MediaProvider.Tmdb), ("Inception", Poster: false, MediaProvider.Tmdb));
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenItemsLoad_CachesTheirMetadata()
	{
		await host.WithMdblistAccount("test-key");
		var fightClub = await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb().SeedAsync();
		host.MdblistFeedContains("top-250", Item("Fight Club", MediaType.Movie, Tmdb("550")));

		await Testee.GetFeedItemsAsync("top-250").ToListAsync();

		host.ScopedDbContext.ChangeTracker.Clear();
		var metadata = await host.ScopedDbContext.MediaMetadata.AsNoTracking().FirstAsync(entry => entry.MediaId == fightClub.Value);
		await Assert.That(metadata.Title).IsEqualTo("Fight Club");
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenAnItemAddsIds_MergesThemIntoTheExistingEntry()
	{
		await host.WithMdblistAccount("test-key");
		var fightClub = await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb().SeedAsync();
		host.MdblistFeedContains("top-250", Item("Fight Club", MediaType.Movie, Tmdb("550"), Imdb("tt0137523")));

		await Testee.GetFeedItemsAsync("top-250").ToListAsync();

		await AssertExternalIdStored(fightClub, MediaProvider.Imdb, "tt0137523");
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenTheFeedTmdbIdConflictsWithTheRow_PrefersTheProvider()
	{
		await host.WithMdblistAccount("test-key");
		var inception = await host.Movie(Spec("Inception", Tmdb("27205"), Imdb("tt1375666"), Plex("27205"))).SeedAsync();
		host.PlexHolds(Spec("Inception", Tmdb("27205"), Imdb("tt1375666"), Plex("27205")));
		await host.PlexSearchReturns("Inception", Spec("Inception", Plex("27205")));
		host.MdblistFeedContains("conflict-list", Item("Inception", MediaType.Movie, Tmdb("99999")));

		await Testee.GetFeedItemsAsync("conflict-list").ToListAsync();

		await AssertExternalIdStored(inception, MediaProvider.Tmdb, "27205");
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenTheFeedMixesTypes_SurfacesMoviesAndShows()
	{
		await host.WithMdblistAccount("test-key");
		await host.Movie(Spec("Inception", Tmdb("27205"), Imdb("tt1375666"), Plex("27205"))).ServedByPlex().ServedByTmdb().SeedAsync();
		await host.Show(TestMedia.BreakingBad).ServedByPlex().ServedByTmdb().SeedAsync();
		host.MdblistFeedContains("mixed-list", Item("Inception", MediaType.Movie, Tmdb("27205")), Item("Breaking Bad", MediaType.Show, Tmdb("1396")));

		await Assert.That(await Testee.GetFeedItemsAsync("mixed-list").ToListAsync()).Count().IsEqualTo(2);
	}

	[Test]
	public async Task GetFeedItemsAsync_WhenTheItemTitleDiffersFromTheCachedRow_StillMatchesByExternalId()
	{
		await host.WithMdblistAccount("test-key");
		var inception = await host.Movie(Spec("Inception", Tmdb("27205"), Imdb("tt1375666"), Plex("27205"))).ServedByPlex().ServedByTmdb().SeedAsync();
		host.MdblistFeedContains("fuzzy-list", Item("INCEPTION (2010)", MediaType.Movie, Tmdb("27205")));

		await Testee.GetFeedItemsAsync("fuzzy-list").ToListAsync();

		await AssertExternalIdStored(inception, MediaProvider.Tmdb, "27205");
	}

	private static MediaSpec Spec(string title, params ProviderId[] ids) => new(title, MediaType.Movie, ids);

	private static FeedItemSpec Item(string? title, MediaType type, params ProviderId[] ids) =>
		new(title, type, null, ids.ToDictionary(id => id.Provider, id => id.Value));

	private static FeedPage Page(int offset, bool hasMore, params string[] titles) => new(offset, hasMore, titles);

	private static async Task AssertItems(IReadOnlyList<MediaLookup> items, params (string Title, bool Poster, MediaProvider? Rating)[] expected)
	{
		await Assert.That(items).Count().IsEqualTo(expected.Length);
		foreach (var (item, row) in items.Zip(expected))
		{
			await Assert.That(item.Title).IsEqualTo(row.Title);
			if (row.Poster)
			{
				await Assert.That(item.PosterUrl).IsNotEmpty();
			}

			if (row.Rating is { } rating)
			{
				await Assert.That(item.Ratings.Any(entry => entry.Source == rating)).IsTrue();
			}
		}
	}

	private static async Task AssertAllUnmatched(IReadOnlyList<MediaLookup> items) =>
		await Assert.That(items.All(item => !(item is IdentifiedMedia identified && identified is IMedia))).IsTrue();

	private async Task AssertExternalIdStored(MediaId mediaId, MediaProvider provider, string id)
	{
		host.ScopedDbContext.ChangeTracker.Clear();
		var exists = await host.ScopedDbContext.ExternalMediaIds.AnyAsync(entry => entry.Media.Id == mediaId.Value && entry.Source == provider && entry.Id == id);
		await Assert.That(exists).IsTrue();
	}
}
