using Contracts.Media;
using Contracts.Media.Metadata;
using Integration.Tests.Infrastructure.Hosting;
using Integration.Tests.Infrastructure.Media;
using Integration.Tests.Infrastructure.Media.Tmdb;
using Reqnroll.Assist;

namespace Integration.Tests.Browse;

[Binding]
public class TmdbRatingSteps(TmdbRatingScenarioContext ctx, SammlarrScenarioContext sammlarr, MediaCatalogContext catalog, TmdbContext tmdb)
{
	[When("I fetch ratings for {string}")]
	public async Task WhenFetchRatings(string title)
	{
		var entry = catalog[title];
		var resolved = tmdb.Versions.GetValueOrDefault(title) ?? MediaVersionResolver.Resolve(entry, new MediaRow());
		var tmdbId = resolved.Ids.GetValueOrDefault(MediaProvider.Tmdb, "0");
		ctx.Ratings = await sammlarr.ResolveKeyed<IMediaRatingService>(RatingProvider.Tmdb).GetRatingsAsync(TmdbId.From(tmdbId, entry.MediaType));
	}

	[When("I fetch ratings using an IMDb ID")]
	public async Task WhenFetchRatingsWithImdbId() =>
		ctx.Ratings = await sammlarr.ResolveKeyed<IMediaRatingService>(RatingProvider.Tmdb).GetRatingsAsync(ImdbId.From("tt0137523", MediaType.Movie));

	[Then("the ratings are:")]
	public void ThenRatingsAre(DataTable table) =>
		table.CompareToSet(ctx.Ratings!.Select(r => new RatingRow(r.Source, r.NormalizedValue, r.VoteCount ?? 0)), true);

	[Then("the result is empty")]
	public async Task ThenResultIsEmpty() => await Assert.That(ctx.Ratings!).IsEmpty();

	private sealed record RatingRow(RatingSource Source, float Value, int Votes);
}
