using System.Text.Json;
using Contracts.Feed;
using Contracts.Media.Search;
using Domain.Feed;
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
	private IFeedService Testee => host.ResolveKeyed<IFeedService>(FeedProvider.Mdblist);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Loading_a_feed_without_an_api_key_throws()
	{
		host.MdblistFeedContains("top-250", Item("Fight Club", MediaType.Movie, Tmdb("550")));

		await Assert.That(async () => await Testee.GetFeedItemsAsync("top-250").ToListAsync()).Throws<Exception>().WithMessageContaining("No MDBList API key");
	}

	[Test]
	public async Task A_feed_that_returns_a_server_error_surfaces_the_failure()
	{
		await host.WithMdblistAccount("test-key");
		host.MdblistFeedReturnsStatus("broken-list", System.Net.HttpStatusCode.InternalServerError);

		await Assert.That(async () => await Testee.GetFeedItemsAsync("broken-list").ToListAsync()).Throws<Exception>().WithMessageContaining("500");
	}

	[Test]
	public async Task A_feed_that_returns_a_malformed_body_fails_to_load()
	{
		await host.WithMdblistAccount("test-key");
		host.MdblistFeedReturnsInvalidJson("corrupt-list");

		await Assert.That(async () => await Testee.GetFeedItemsAsync("corrupt-list").ToListAsync()).Throws<JsonException>();
	}

	[Test]
	public async Task A_feed_whose_provider_circuit_is_open_surfaces_a_typed_failure()
	{
		await host.WithMdblistAccount("test-key");
		host.MdblistFeedUnavailable("storm-list");

		await Assert.That(async () => await Testee.GetFeedItemsAsync("storm-list").ToListAsync()).Throws<ProviderUnavailableException>();
	}

	[Test]
	public async Task A_feed_that_becomes_unavailable_mid_load_still_yields_the_items_fetched_before_the_failure()
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
	public async Task A_feed_that_returns_no_movies_or_shows_loads_no_items()
	{
		await host.WithMdblistAccount("test-key");
		host.MdblistFeedEmpty("empty-list");

		await Assert.That(await Testee.GetFeedItemsAsync("empty-list").ToListAsync()).IsEmpty();
	}

	[Test]
	public async Task A_multi_page_feed_loads_every_page()
	{
		await host.WithMdblistAccount("test-key");
		host.MdblistFeedPaginated("big-list", Page(0, hasMore: true, "Item1", "Item2"), Page(1000, hasMore: false, "Item3"));

		await Assert.That(await Testee.GetFeedItemsAsync("big-list").ToListAsync()).Count().IsEqualTo(3);
	}

	[Test]
	public async Task Duplicate_feed_entries_are_preserved_in_order()
	{
		await host.WithMdblistAccount("test-key");
		await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb().SeedAsync();
		host.MdblistFeedContains("dupes-list", Item("Fight Club", MediaType.Movie, Tmdb("550")), Item("Fight Club", MediaType.Movie, Tmdb("550")));

		await Assert.That(await Testee.GetFeedItemsAsync("dupes-list").ToListAsync()).Count().IsEqualTo(2);
	}

	[Test]
	public async Task A_feed_whose_items_all_fail_strict_validation_returns_them_as_unresolved()
	{
		await host.WithMdblistAccount("test-key");
		host.MdblistFeedContains("loose-list", Item("No Ids One", MediaType.Movie), Item("No Ids Two", MediaType.Movie));

		var items = await Testee.GetFeedItemsAsync("loose-list").ToListAsync();

		await Assert.That(items).Count().IsEqualTo(2);
		await AssertAllUnresolved(items);
	}

	[Test]
	public async Task A_feed_item_whose_metadata_cannot_be_resolved_is_returned_as_unresolved()
	{
		await host.WithMdblistAccount("test-key");
		await host.WithPlexAccount("plex-token");
		await host.Movie(TestMedia.FightClub).SeedAsync();
		host.PlexLookupFails(TestMedia.FightClub);
		host.MdblistFeedContains("failing-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));

		var items = await Testee.GetFeedItemsAsync("failing-list").ToListAsync();

		await Assert.That(items).Count().IsEqualTo(1);
		await AssertAllUnresolved(items);
	}

	[Test]
	public async Task Feed_item_with_no_known_ids_at_all_is_still_returned()
	{
		await host.WithMdblistAccount("test-key");
		host.MdblistFeedContains("mystery-list", Item("Unknown Movie", MediaType.Movie));

		var items = await Testee.GetFeedItemsAsync("mystery-list").ToListAsync();

		await AssertItems(items, ("Unknown Movie", Poster: false, Rating: null));
	}

	[Test]
	public async Task Feed_item_missing_release_year_is_tolerated()
	{
		await host.WithMdblistAccount("test-key");
		host.MdblistFeedContains("no-year-list", Item("Orphaned", MediaType.Movie));

		var items = await Testee.GetFeedItemsAsync("no-year-list").ToListAsync();

		await AssertItems(items, ("Orphaned", Poster: false, Rating: null));
	}

	[Test]
	public async Task Feed_item_with_explicit_null_title_is_tolerated()
	{
		await host.WithMdblistAccount("test-key");
		host.MdblistFeedContains("null-title-list", Item(null, MediaType.Movie, Tmdb("4242")));

		await Assert.That(await Testee.GetFeedItemsAsync("null-title-list").ToListAsync()).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Feed_item_known_only_by_imdb_id_is_still_returned()
	{
		await host.WithMdblistAccount("test-key");
		host.PlexHolds(Spec("Inception", Tmdb("27205"), Imdb("tt1375666"), Plex("27205")));
		await host.PlexSearchReturns("Inception", Spec("Inception", Plex("27205")));
		host.MdblistFeedContains("imdb-only-list", Item("Inception", MediaType.Movie, Imdb("tt1375666")));

		var items = await Testee.GetFeedItemsAsync("imdb-only-list").ToListAsync();

		await AssertItems(items, ("Inception", Poster: true, Rating: null));
	}

	[Test]
	public async Task Loaded_feed_item_is_enriched_with_metadata_and_rating()
	{
		await host.WithMdblistAccount("test-key");
		await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb(rating: 8.4, votes: 23000).SeedAsync();
		host.MdblistFeedContains("top-250", Item("Fight Club", MediaType.Movie, Tmdb("550")));

		var items = await Testee.GetFeedItemsAsync("top-250").ToListAsync();

		await AssertItems(items, ("Fight Club", Poster: true, RatingProvider.Tmdb));
	}

	[Test]
	public async Task Feed_with_multiple_movies_resolves_all_items()
	{
		await host.WithMdblistAccount("test-key");
		await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb(rating: 8.4, votes: 23000).SeedAsync();
		await host.Movie(Spec("Inception", Tmdb("27205"), Imdb("tt1375666"), Plex("27205"))).ServedByPlex().ServedByTmdb(rating: 7.8, votes: 15000).SeedAsync();
		host.MdblistFeedContains("top-250", Item("Fight Club", MediaType.Movie, Tmdb("550")), Item("Inception", MediaType.Movie, Tmdb("27205")));

		var items = await Testee.GetFeedItemsAsync("top-250").ToListAsync();

		await AssertItems(items, ("Fight Club", Poster: false, RatingProvider.Tmdb), ("Inception", Poster: false, RatingProvider.Tmdb));
	}

	[Test]
	public async Task Loading_a_feed_caches_the_items_metadata()
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
	public async Task Feed_item_with_additional_ids_merges_new_identifiers_into_existing_entry()
	{
		await host.WithMdblistAccount("test-key");
		var fightClub = await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb().SeedAsync();
		host.MdblistFeedContains("top-250", Item("Fight Club", MediaType.Movie, Tmdb("550"), Imdb("tt0137523")));

		await Testee.GetFeedItemsAsync("top-250").ToListAsync();

		await AssertExternalIdStored(fightClub, MediaProvider.Imdb, "tt0137523");
	}

	[Test]
	public async Task Feed_tmdb_id_conflicting_with_the_resolved_row_is_overridden_by_the_provider()
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
	public async Task Mixed_type_feed_surfaces_movies_and_shows()
	{
		await host.WithMdblistAccount("test-key");
		await host.Movie(Spec("Inception", Tmdb("27205"), Imdb("tt1375666"), Plex("27205"))).ServedByPlex().ServedByTmdb().SeedAsync();
		await host.Show(TestMedia.BreakingBad).ServedByPlex().ServedByTmdb().SeedAsync();
		host.MdblistFeedContains("mixed-list", Item("Inception", MediaType.Movie, Tmdb("27205")), Item("Breaking Bad", MediaType.Show, Tmdb("1396")));

		await Assert.That(await Testee.GetFeedItemsAsync("mixed-list").ToListAsync()).Count().IsEqualTo(2);
	}

	[Test]
	public async Task Feed_item_title_differs_from_cached_row_title()
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

	private static async Task AssertItems(IReadOnlyList<MediaLookup> items, params (string Title, bool Poster, RatingProvider? Rating)[] expected)
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

	private static async Task AssertAllUnresolved(IReadOnlyList<MediaLookup> items) =>
		await Assert.That(items.All(item => !(item is IdentifiedMedia identified && identified is IMedia))).IsTrue();

	private async Task AssertExternalIdStored(MediaId mediaId, MediaProvider provider, string id)
	{
		host.ScopedDbContext.ChangeTracker.Clear();
		var exists = await host.ScopedDbContext.ExternalMediaIds.AnyAsync(entry => entry.Media.Id == mediaId.Value && entry.Source == provider && entry.Id == id);
		await Assert.That(exists).IsTrue();
	}
}
