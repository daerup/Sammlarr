using System.Net;
using Contracts;
using Contracts.Media;
using Contracts.Media.Trending;
using Domain.Media;
using Domain.Providers;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Browse;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class TmdbTrendingTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task GetTrendingAsync_WhenAMovieIsCached_ResolvesIt()
	{
		await host.TmdbTrendingPage(1, TestMedia.Inception);
		await host.Movie(TestMedia.Inception).SeedAsync();

		var items = (await FetchTrending(1)).Items;

		await Assert.That(items).Count().IsEqualTo(1);
		await AssertResolvedAs(items[0], TestMedia.Inception);
	}

	[Test]
	public async Task GetTrendingAsync_ForACachedMovie_AddsTheRatingsSammlarrStoresForIt()
	{
		await host.TmdbTrendingPage(1, TestMedia.Inception);
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();
		var stored = new MovieDetails { Title = "Inception", Ratings = MediaRatings.From([new ImdbRating(8.8f, Some.Of(2_700_000))]) };
		await host.Resolve<IMediaRepository>()
			.SaveDetailsAsync([new DetailsChange(inception, new None(), new CachedDetails(stored, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, []))]);

		var items = (await FetchTrending(1)).Items;

		await Assert
			.That(items[0])
			.IsOfUnionType<MediaMatchResult, MatchedMedia>()
			.And.Multiple(async matched =>
			{
				await Assert.That(matched.Details.Ratings.Of(RatingSource.Imdb)).IsSome();
				await Assert.That(matched.Details.Ratings.Of(RatingSource.Tmdb)).IsSome();
			});
	}

	[Test]
	public async Task GetTrendingAsync_WhenAShowIsCached_ResolvesIt()
	{
		await host.TmdbTrendingPage(1, TestMedia.BreakingBad);
		await host.Show(TestMedia.BreakingBad).SeedAsync();

		var items = (await FetchTrending(1)).Items;

		await Assert.That(items).Count().IsEqualTo(1);
		await AssertResolvedAs(items[0], TestMedia.BreakingBad);
	}

	[Test]
	public async Task GetTrendingAsync_WhenAMovieIsUncached_ResolvesItThroughPlex()
	{
		await host.TmdbTrendingPage(1, TestMedia.Inception);
		host.PlexHolds(TestMedia.Inception);
		await host.PlexSearchReturns("Inception", TestMedia.Inception);

		var items = (await FetchTrending(1)).Items;

		await Assert.That(items).Count().IsEqualTo(1);
		await AssertResolvedAs(items[0], TestMedia.Inception);
	}

	[Test]
	public async Task GetTrendingAsync_WhenPlexCannotResolveTheMovie_LeavesItUnmatched()
	{
		await host.TmdbTrendingPage(1, TestMedia.Inception);
		await host.PlexFindsNothing("Inception");
		host.TmdbDetailFails(TestMedia.Inception, HttpStatusCode.NotFound);

		var items = (await FetchTrending(1)).Items;

		await Assert.That(items).Count().IsEqualTo(1);
		await AssertUnmatched(items[0], "Inception");
	}

	[Test]
	public async Task GetTrendingAsync_WhenTheMediaTypeIsUnknown_LeavesItUnmatched()
	{
		await host.TmdbTrendingPage(1, TestMedia.Person("Some Person"));

		var items = (await FetchTrending(1)).Items;

		await Assert.That(items).Count().IsEqualTo(1);
		await AssertUnmatched(items[0], "Some Person");
	}

	[Test]
	public async Task GetTrendingAsync_WhenThePageIsEmpty_ReturnsNoItems()
	{
		await host.TmdbTrendingPage(1);

		await Assert.That((await FetchTrending(1)).Items).IsEmpty();
	}

	[Test]
	public async Task GetTrendingAsync_WhenItemsMixResolvedAndUnmatched_PreservesTheirOrder()
	{
		await host.TmdbTrendingPage(1, TestMedia.Inception, TestMedia.Person("Christopher Nolan"), TestMedia.Interstellar);
		await host.Movie(TestMedia.Inception).SeedAsync();
		host.PlexHolds(TestMedia.Interstellar);
		await host.PlexSearchReturns("Interstellar", TestMedia.Interstellar);

		var items = (await FetchTrending(1)).Items;

		await Assert.That(items).Count().IsEqualTo(3);
		await AssertResolvedAs(items[0], TestMedia.Inception);
		await AssertUnmatched(items[1], "Christopher Nolan");
		await AssertResolvedAs(items[2], TestMedia.Interstellar);
	}

	private async Task<PagedResult<MediaMatchResult>> FetchTrending(int page)
	{
		return await host.ResolveKeyed<ITrendingService>(TrendingProvider.Tmdb).GetTrendingAsync(page);
	}

	private static async Task AssertResolvedAs(MediaMatchResult item, MediaSpec spec)
	{
		await Assert.That(item).IsOfUnionType<MediaMatchResult, MatchedMedia>();
		await Assert.That(item.Details.Title).IsEqualTo(spec.Title);
		foreach (var id in spec.Ids)
		{
			await Assert.That(item.Externals.Any(external => external.Source == id.Provider && external.Value == id.Value)).IsTrue();
		}
	}

	private static async Task AssertUnmatched(MediaMatchResult item, string title)
	{
		await Assert.That(item).IsOfUnionType<MediaMatchResult, UnmatchedMedia>();
		await Assert.That(item.Details.Title).IsEqualTo(title);
	}
}
