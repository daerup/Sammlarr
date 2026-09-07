using Contracts;
using Contracts.Media.Trending;
using Domain.Media;
using Domain.Providers;
using Harness;
using Integration.Tests.Assertions;
using TestInfrastructure;

namespace Integration.Tests.Browse;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class TmdbTrendingTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task A_cached_movie_resolves()
	{
		await host.TmdbTrendingPage(1, TestMedia.Inception);
		await host.Movie(TestMedia.Inception).SeedAsync();

		var items = (await FetchTrending(1)).Items;

		await Assert.That(items).Count().IsEqualTo(1);
		await AssertResolvedAs(items[0], TestMedia.Inception);
	}

	[Test]
	public async Task A_cached_show_resolves()
	{
		await host.TmdbTrendingPage(1, TestMedia.BreakingBad);
		await host.Show(TestMedia.BreakingBad).SeedAsync();

		var items = (await FetchTrending(1)).Items;

		await Assert.That(items).Count().IsEqualTo(1);
		await AssertResolvedAs(items[0], TestMedia.BreakingBad);
	}

	[Test]
	public async Task An_uncached_movie_is_resolved_through_plex()
	{
		await host.TmdbTrendingPage(1, TestMedia.Inception);
		host.PlexHolds(TestMedia.Inception);
		await host.PlexSearchReturns("Inception", TestMedia.Inception);

		var items = (await FetchTrending(1)).Items;

		await Assert.That(items).Count().IsEqualTo(1);
		await AssertResolvedAs(items[0], TestMedia.Inception);
	}

	[Test]
	public async Task An_uncached_movie_plex_cannot_resolve_stays_unresolved()
	{
		await host.TmdbTrendingPage(1, TestMedia.Inception);
		await host.PlexFindsNothing("Inception");

		var items = (await FetchTrending(1)).Items;

		await Assert.That(items).Count().IsEqualTo(1);
		await AssertUnresolved(items[0], "Inception");
	}

	[Test]
	public async Task An_unknown_media_type_stays_unresolved()
	{
		await host.TmdbTrendingPage(1, TestMedia.Person("Some Person"));

		var items = (await FetchTrending(1)).Items;

		await Assert.That(items).Count().IsEqualTo(1);
		await AssertUnresolved(items[0], "Some Person");
	}

	[Test]
	public async Task An_empty_page_returns_no_items()
	{
		await host.TmdbTrendingPage(1);

		await Assert.That((await FetchTrending(1)).Items).IsEmpty();
	}

	[Test]
	public async Task Item_order_is_preserved_across_resolved_and_unresolved()
	{
		await host.TmdbTrendingPage(1, TestMedia.Inception, TestMedia.Person("Christopher Nolan"), TestMedia.Interstellar);
		await host.Movie(TestMedia.Inception).SeedAsync();
		host.PlexHolds(TestMedia.Interstellar);
		await host.PlexSearchReturns("Interstellar", TestMedia.Interstellar);

		var items = (await FetchTrending(1)).Items;

		await Assert.That(items).Count().IsEqualTo(3);
		await AssertResolvedAs(items[0], TestMedia.Inception);
		await AssertUnresolved(items[1], "Christopher Nolan");
		await AssertResolvedAs(items[2], TestMedia.Interstellar);
	}

	private async Task<PagedResult<MediaLookup>> FetchTrending(int page) =>
		await host.ResolveKeyed<ITrendingService>(TrendingProvider.Tmdb).GetTrendingAsync(page);

	private static async Task AssertResolvedAs(MediaLookup item, MediaSpec spec)
	{
		await Assert.That(item).IsOfUnionType<MediaLookup, IdentifiedMedia>();
		await Assert.That(item.Title).IsEqualTo(spec.Title);
		foreach (var id in spec.Ids)
		{
			await Assert.That(item.Externals.Any(external => external.Source == id.Provider && external.Value == id.Value)).IsTrue();
		}
	}

	private static async Task AssertUnresolved(MediaLookup item, string title)
	{
		await Assert.That(item).IsOfUnionType<MediaLookup, UnmatchedMedia>();
		await Assert.That(item.Title).IsEqualTo(title);
	}
}
