using Contracts.Media;
using Contracts.Ratings;
using Domain.Media;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Ratings;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class RottenTomatoesPagesTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string Imdb = "tt1375666";

	private static readonly RatingSummary Rated = RatingSummary.From([new RottenTomatoesCriticsRating(87), new RottenTomatoesAudienceRating(91)]);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	private IRottenTomatoesPages Testee => host.Resolve<IRottenTomatoesPages>();

	[Test]
	public async Task FindPathAsync_ForATitleWikidataKnows_AsksOnceAndKeepsItsPage()
	{
		var inception = await SeedRatedAsync();
		host.WikidataHolds(Imdb, "m/inception");

		await Testee.FindPathAsync(inception);
		var again = await Testee.FindPathAsync(inception);

		await Assert.That(again).IsSome().And.IsEqualTo("m/inception");
		await Assert.That(host.WikidataAsked(Imdb)).IsEqualTo(1);
	}

	[Test]
	public async Task FindPathAsync_WhenWikidataHasNoPage_KeepsThatAndNeverAsksAgain()
	{
		var inception = await SeedRatedAsync();
		host.WikidataHolds(Imdb);

		await Testee.FindPathAsync(inception);
		var again = await Testee.FindPathAsync(inception);

		await Assert.That(again).IsNone();
		await Assert.That(host.WikidataAsked(Imdb)).IsEqualTo(1);
	}

	[Test]
	public async Task FindPathAsync_AfterTheTitlesRatingsAreRewritten_StillKnowsItsPage()
	{
		var inception = await SeedRatedAsync();
		host.WikidataHolds(Imdb, "m/inception");
		await Testee.FindPathAsync(inception);

		await StoreAsync(inception, RatingSummary.From([new RottenTomatoesCriticsRating(88), new RottenTomatoesAudienceRating(91)]));

		await Assert.That(await Testee.FindPathAsync(inception)).IsSome().And.IsEqualTo("m/inception");
		await Assert.That(host.WikidataAsked(Imdb)).IsEqualTo(1);
	}

	[Test]
	public async Task FindPathAsync_WhenWikidataIsDown_KeepsNothingAndAsksAgainNextTime()
	{
		var inception = await SeedRatedAsync();
		host.WikidataFails(Imdb);
		var whileDown = await Testee.FindPathAsync(inception);

		host.WikidataHolds(Imdb, "m/inception");

		await Assert.That(whileDown).IsNone();
		await Assert.That(await Testee.FindPathAsync(inception)).IsSome().And.IsEqualTo("m/inception");
	}

	private async Task<MediaId> SeedRatedAsync()
	{
		var inception = await host.Movie("Inception", tmdb: "27205", imdb: Imdb).SeedAsync();
		await StoreAsync(inception, Rated);
		return inception;
	}

	private Task StoreAsync(MediaId id, RatingSummary ratings)
	{
		var details = new MovieDetails { Title = "Inception", Rating = ratings };
		return host.Resolve<IMediaRepository>()
			.UpsertDetailsAsync(new Dictionary<MediaId, CachedDetails> { [id] = new(details, DateTimeOffset.UtcNow, MediaField.None) });
	}
}
