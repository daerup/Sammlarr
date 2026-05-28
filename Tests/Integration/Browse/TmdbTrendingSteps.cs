using Contracts.Media.Trending;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Media;

namespace Integration.Tests.Browse;

[Binding]
public class TmdbTrendingSteps(TmdbTrendingScenarioContext ctx, SammlarrScenarioContext sammlarr, MediaCatalogContext catalog)
{
	[Given("the TMDB trending endpoint includes {string}")]
	public void GivenTrendingEndpointIncludes(string key) => ctx.TrendingItems.Add(catalog[key]);

	[When("I request trending page {int}")]
	public async Task WhenRequestTrendingPage(int page)
	{
		sammlarr.HttpHandler.RespondWithJson(BuildTrendingJson(ctx.TrendingItems));
		ctx.Result = await sammlarr.ResolveKeyed<IMediaRecommendationService>(MediaRecommendationProvider.Tmdb).GetTrendingAsync(page: page);
	}

	[Then(@"the result contains {int} item\(s\)")]
	public async Task ThenResultContainsItems(int count) => await Assert.That(ctx.Result!.Items.Count).IsEqualTo(count);

	[Then("item {int} is a resolved TrendingMedia with title {string}")]
	public async Task ThenItemIsResolvedTrendingMedia(int position, string title)
	{
		var item = ctx.Result!.Items[position - 1];
		await Assert.That(item).IsTypeOf<TrendingMedia>();
		await Assert.That(item.Title).IsEqualTo(title);
	}

	[Then("item {int} is an UnresolvedTrendingMedia with title {string}")]
	public async Task ThenItemIsUnresolvedTrendingMedia(int position, string title)
	{
		var item = ctx.Result!.Items[position - 1];
		await Assert.That(item).IsTypeOf<UnresolvedTrendingMedia>();
		await Assert.That(item.Title).IsEqualTo(title);
	}

	[Then("item {int} has a rating")]
	public async Task ThenItemHasRating(int position)
	{
		var item = ctx.Result!.Items[position - 1];
		await Assert.That(item.Rating).IsNotNull();
		await Assert.That(item.Rating!.Value).IsGreaterThan(0f);
	}

	[Then("item {int} has a poster URL")]
	public async Task ThenItemHasPosterUrl(int position) => await Assert.That(ctx.Result!.Items[position - 1].PosterUrl).IsNotEmpty();

	[Then("item {int} has a description")]
	public async Task ThenItemHasDescription(int position) => await Assert.That(ctx.Result!.Items[position - 1].Description).IsNotEmpty();

	private static string BuildTrendingJson(IReadOnlyList<RegisteredMedia> items)
	{
		var results = string.Join(
			",",
			items.Select(item =>
				$$"""
				{
				  "adult": false,
				  "backdrop_path": "/backdrop.jpg",
				  "id": {{item.TmdbId}},
				  "title": "{{item.Title}}",
				  "original_title": "{{item.Title}}",
				  "overview": "An overview",
				  "poster_path": "/poster.jpg",
				  "media_type": "{{item.TmdbMediaType}}",
				  "original_language": "en",
				  "genre_ids": [28],
				  "popularity": 1.5,
				  "release_date": "2024-01-15",
				  "video": false,
				  "vote_average": 7.5,
				  "vote_count": 100
				}
				"""
			)
		);
		return $$"""
			{
			  "page": 1,
			  "results": [{{results}}],
			  "total_pages": 1,
			  "total_results": {{items.Count}}
			}
			""";
	}
}
