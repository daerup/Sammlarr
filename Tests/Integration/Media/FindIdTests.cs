using Contracts.Media;
using Domain.Media;
using Domain.Providers;
using Harness;
using TestInfrastructure;
using static Harness.Ids;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class FindIdTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string ImdbTitle = "tt1375666";

	private static readonly MediaSpec Inception = new("Inception", MediaType.Movie, [Tmdb("27205"), Imdb(ImdbTitle)]);

	private IMediaService Testee => host.Resolve<IMediaService>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task FindIdAsync_ForATitleWikidataKnows_FindsItsRottenTomatoesIdOnceAndThenReadsItBack()
	{
		var inception = await host.Movie(Inception).SeedAsync();
		host.WikidataHolds(ImdbTitle, "m/inception");

		await Testee.FindIdAsync(inception, MediaProvider.RottenTomatoes);
		var again = await Testee.FindIdAsync(inception, MediaProvider.RottenTomatoes);

		await Assert.That(again).IsSome().And.IsEqualTo(RottenTomatoesId.From("m/inception", MediaType.Movie));
		await Assert.That(host.WikidataAsked(ImdbTitle)).IsEqualTo(1);
	}

	[Test]
	public async Task FindIdAsync_WhenWikidataHasNoPage_FindsNothingAndDoesNotAskAgainSoon()
	{
		var inception = await host.Movie(Inception).SeedAsync();
		host.WikidataHolds(ImdbTitle);

		await Testee.FindIdAsync(inception, MediaProvider.RottenTomatoes);
		var again = await Testee.FindIdAsync(inception, MediaProvider.RottenTomatoes);

		await Assert.That(again).IsNone();
		await Assert.That(host.WikidataAsked(ImdbTitle)).IsEqualTo(1);
	}

	[Test]
	public async Task FindIdAsync_WhenWikidataIsDown_FindsNothingAndAsksAgainNextTime()
	{
		var inception = await host.Movie(Inception).SeedAsync();
		host.WikidataFails(ImdbTitle);
		var whileDown = await Testee.FindIdAsync(inception, MediaProvider.RottenTomatoes);

		host.WikidataHolds(ImdbTitle, "m/inception");

		await Assert.That(whileDown).IsNone();
		await Assert.That(await Testee.FindIdAsync(inception, MediaProvider.RottenTomatoes)).IsSome();
	}

	[Test]
	public async Task FindIdAsync_WhenThePageAlreadyBelongsToAnotherTitle_FindsNothingAndKeepsBothTitlesApart()
	{
		var original = await host.Movie(Inception).SeedAsync();
		var remake = await host.Movie(new MediaSpec("Inception", MediaType.Movie, [Tmdb("999"), Imdb("tt9999999")])).SeedAsync();
		host.WikidataHolds(ImdbTitle, "m/inception");
		host.WikidataHolds("tt9999999", "m/inception");
		await Testee.FindIdAsync(original, MediaProvider.RottenTomatoes);

		var found = await Testee.FindIdAsync(remake, MediaProvider.RottenTomatoes);

		await Assert.That(found).IsNone();
		var stored = await host.Resolve<IMediaRepository>().GetAsync([original, remake]);
		await Assert.That(stored.Select(media => media.Identity.Id)).IsEquivalentTo([original, remake]);
		await Assert.That(stored.Single(media => media.Identity.Id == remake).Identity.Externals.OfProvider(MediaProvider.RottenTomatoes)).IsNone();
	}

	[Test]
	public async Task FindIdAsync_ForATitleKnownOnlyByItsTmdbId_TakesTheImdbIdFromTmdbFirst()
	{
		var inception = await host.Movie(new MediaSpec("Inception", MediaType.Movie, [Tmdb("27205")])).SeedAsync();
		await host.TmdbKnowsTheExternalIdsOf(Inception);
		host.WikidataHolds(ImdbTitle, "m/inception");

		var found = await Testee.FindIdAsync(inception, MediaProvider.RottenTomatoes);

		await Assert.That(found).IsSome().And.IsEqualTo(RottenTomatoesId.From("m/inception", MediaType.Movie));
	}
}
