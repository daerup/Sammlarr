using API.Mdblist;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Watchlist;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Settings;

[Binding]
public class ProviderKeyValidationSteps(ProviderKeyValidationScenarioContext ctx, SammlarrScenarioContext sammlarr, ExceptionContext exceptionContext)
{
	[Given("the TMDB API returns rating data for movie {int}")]
	public void GivenTmdbReturnsRatingData(int movieId) =>
		sammlarr.HttpHandler.RespondWithJson(
			$$"""
			{
			  "id": {{movieId}},
			  "title": "Test Movie",
			  "name": null,
			  "media_type": "movie",
			  "vote_average": 7.8,
			  "vote_count": 5000
			}
			"""
		);

	[When("I request TMDB ratings for movie {int}")]
	public async Task WhenRequestTmdbRatings(int movieId) =>
		ctx.TmdbRatings = await sammlarr
			.ResolveKeyed<IMediaRatingService>(RatingProvider.Tmdb)
			.GetRatingsAsync(TmdbId.From(movieId.ToString(), MediaType.Movie));

	[When("I send a request to the MdbList user endpoint")]
	public async Task WhenSendMdblistRequest() =>
		await exceptionContext.TryExecuteAsync(async () => ctx.MdblistResponse = await sammlarr.Resolve<MdblistClient>().SendAsync(HttpMethod.Get, "user"));

	[When("I add Plex item {string} to the watchlist")]
	public async Task WhenAddPlexItem(string plexId)
	{
		var provider = sammlarr.ResolveKeyed<IWatchlistProvider>(WatchlistProvider.Plex);
		await foreach (var result in provider.AddToWatchlistAsync([PlexId.From(plexId, MediaType.Movie)]))
		{
			ctx.PlexPushResult = result;
		}
	}

	[Then("no TMDB ratings are returned")]
	public async Task ThenNoTmdbRatings() => await Assert.That(ctx.TmdbRatings!).IsEmpty();

	[Then("TMDB ratings are returned")]
	public async Task ThenTmdbRatingsReturned() => await Assert.That(ctx.TmdbRatings!).IsNotEmpty();

	[Then("the MdbList request succeeds")]
	public async Task ThenMdblistSucceeded() => await Assert.That(ctx.MdblistResponse!.IsSuccessStatusCode).IsTrue();

	[Then("the Plex push fails")]
	public async Task ThenPlexPushFails() => await Assert.That(ctx.PlexPushResult!.Value.Success).IsFalse();

	[Then("the Plex push succeeds")]
	public async Task ThenPlexPushSucceeds() => await Assert.That(ctx.PlexPushResult!.Value.Success).IsTrue();
}
