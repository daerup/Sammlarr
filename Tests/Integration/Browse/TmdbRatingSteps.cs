using Contracts.Media;
using Contracts.Media.Metadata;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Media;

namespace Integration.Tests.Browse;

[Binding]
public class TmdbRatingSteps(TmdbRatingScenarioContext ctx, SammlarrScenarioContext sammlarr, MediaCatalogContext catalog)
{
	[Given("the TMDB API returns vote average {double} and vote count {int} for {string}")]
	public void GivenTmdbVoteData(double voteAverage, int voteCount, string key)
	{
		var media = catalog[key];
		var json = $$"""
			{
			  "id": {{media.TmdbId}},
			  "title": "{{media.Title}}",
			  "name": null,
			  "media_type": "movie",
			  "vote_average": {{voteAverage}},
			  "vote_count": {{voteCount}}
			}
			""";
		sammlarr.HttpHandler.RespondWithJsonForUrl($"movie/{media.TmdbId}", json);
	}

	[Given("the TMDB API returns no vote data for {string}")]
	public void GivenTmdbNoVoteData(string key)
	{
		var media = catalog[key];
		sammlarr.HttpHandler.RespondWithJson(
			$$"""
			{
			  "id": {{media.TmdbId}},
			  "title": "{{media.Title}}",
			  "name": null,
			  "media_type": "movie"
			}
			"""
		);
	}

	[When("I fetch ratings for {string}")]
	public async Task WhenFetchRatings(string key)
	{
		var media = catalog[key];
		ctx.Result = await sammlarr
			.ResolveKeyed<IMediaRatingService>(RatingProvider.Tmdb)
			.GetRatingsAsync(TmdbId.From(media.TmdbId.ToString(), media.MediaType));
	}

	[When("I fetch ratings using an IMDb ID")]
	public async Task WhenFetchRatingsWithImdbId() =>
		ctx.Result = await sammlarr.ResolveKeyed<IMediaRatingService>(RatingProvider.Tmdb).GetRatingsAsync(ImdbId.From("tt0137523", MediaType.Movie));

	[Then("the result contains {int} rating")]
	public async Task ThenResultContainsRating(int count) => await Assert.That(ctx.Result!.Count).IsEqualTo(count);

	[Then("the rating source is TMDB")]
	public async Task ThenRatingSourceIsTmdb() => await Assert.That(ctx.Result![0].Source).IsEqualTo(RatingSource.Tmdb);

	[Then("the normalized value is {double}")]
	public async Task ThenNormalizedValue(double value) => await Assert.That(ctx.Result![0].NormalizedValue).IsEqualTo((float)value);

	[Then("the vote count is {int}")]
	public async Task ThenVoteCount(int count) => await Assert.That(ctx.Result![0].VoteCount).IsEqualTo(count);

	[Then("the result is empty")]
	public async Task ThenResultIsEmpty() => await Assert.That(ctx.Result!).IsEmpty();
}
