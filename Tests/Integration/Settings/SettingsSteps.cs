using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Watchlist;
using Integration.Tests.Infrastructure.Hosting;
using Integration.Tests.Infrastructure.Http;
using Microsoft.EntityFrameworkCore;
using Test.Shared;

namespace Integration.Tests.Settings;

[Binding]
public class SettingsSteps(SettingsScenarioContext ctx, SammlarrScenarioContext sammlarr)
{
	[Given("the TMDB API key is {string}")]
	public async Task GivenTmdbKeyStored(string apiKey) =>
		await sammlarr.Resolve<IApiKeyConfigRepository>().SetApiKeyAsync(new TestApiKey(ApiKeyProvider.Tmdb, apiKey));

	[When("I look up the TMDB API key")]
	public async Task WhenLookUpTmdbKey() =>
		ctx.RetrievedKey = await sammlarr.Resolve<IApiKeyConfigRepository>().GetApiKeyAsync(ApiKeyProvider.Tmdb) is IApiKey key ? key.Value : null;

	[When("I store the TMDB API key {string}")]
	public async Task WhenStoreTmdbKey(string apiKey) =>
		await sammlarr.Resolve<IApiKeyConfigRepository>().SetApiKeyAsync(new TestApiKey(ApiKeyProvider.Tmdb, apiKey));

	[When("I clear the TMDB API key")]
	public async Task WhenClearTmdbKey() => await sammlarr.Resolve<IApiKeyConfigRepository>().RemoveApiKeyAsync(ApiKeyProvider.Tmdb);

	[Then("no TMDB API key is stored")]
	public async Task ThenNoTmdbKeyStored() => await Assert.That(ctx.RetrievedKey).IsNull();

	[Then("the stored TMDB API key is {string}")]
	public async Task ThenStoredTmdbKeyIs(string expected) => await Assert.That(ctx.RetrievedKey).IsEqualTo(expected);

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
		ctx.Ratings = await sammlarr.ResolveKeyed<IMediaRatingService>(RatingProvider.Tmdb).GetRatingsAsync(TmdbId.From(movieId.ToString(), MediaType.Movie));

	[Given("the TMDB API is unreachable")]
	public void GivenTmdbUnreachable() =>
		sammlarr.HttpHandler.RespondForUrl(TestUrls.TmdbApi, _ => throw new HttpRequestException("The TMDB API is unreachable."));

	[When("I verify the {word} API key {string}")]
	public async Task WhenVerifyKey(string provider, string apiKey) =>
		ctx.KeyValid = await sammlarr.Resolve<IApiKeyFactory>().CreateAsync(Enum.Parse<ApiKeyProvider>(provider, ignoreCase: true), apiKey) is IApiKey;

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
	public async Task ThenNoTmdbRatings() => await Assert.That(ctx.Ratings!).IsEmpty();

	[Then("TMDB ratings are returned")]
	public async Task ThenTmdbRatingsReturned() => await Assert.That(ctx.Ratings!).IsNotEmpty();

	[Then("the {word} key is accepted")]
	public async Task ThenKeyAccepted(string provider) => await Assert.That(ctx.KeyValid).IsTrue();

	[Then("the {word} key is rejected")]
	public async Task ThenKeyRejected(string provider) => await Assert.That(ctx.KeyValid).IsFalse();

	[Then("the Plex push fails")]
	public async Task ThenPlexPushFails() => await Assert.That(ctx.PlexPushResult!.Value.Success).IsFalse();

	[Then("the Plex push succeeds")]
	public async Task ThenPlexPushSucceeds() => await Assert.That(ctx.PlexPushResult!.Value.Success).IsTrue();

	[Then("no TMDB ratings remain cached")]
	public async Task ThenNoTmdbRatingsCached()
	{
		sammlarr.DbContext.ChangeTracker.Clear();
		var any = await sammlarr.DbContext.MediaRatings.AsNoTracking().AnyAsync(r => r.Source == RatingSource.Tmdb);
		await Assert.That(any).IsFalse();
	}
}
