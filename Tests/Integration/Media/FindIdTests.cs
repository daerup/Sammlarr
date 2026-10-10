using Contracts.Media;
using DB.Data.Media;
using Domain.Media;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;
using static Harness.Ids;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class FindIdTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string ImdbTitle = "tt1375666";

	private static readonly MediaSpec Inception = new("Inception", MediaType.Movie, [Tmdb("27205"), Imdb(ImdbTitle)]);

	private static readonly DateTimeOffset Fetched = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);

	private IMediaService Testee => host.Resolve<IMediaService>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task FindIdAsync_ForATitleWikidataKnows_FindsItsRottenTomatoesIdOnceAndThenReadsItBack()
	{
		var inception = await host.Movie(Inception).SeedWithDetailsAsync("Inception", Fetched);
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
	public async Task FindIdAsync_WhenAnotherTitleHasThePage_LinksItFromThisTitleTooAndKeepsBothTitlesApart()
	{
		var original = await host.Movie(Inception).SeedWithDetailsAsync("Inception", Fetched);
		var remake = await host.Movie(new MediaSpec("Inception", MediaType.Movie, [Tmdb("999"), Imdb("tt9999999")])).SeedWithDetailsAsync("Inception", Fetched);
		host.WikidataHolds(ImdbTitle, "m/inception");
		host.WikidataHolds("tt9999999", "m/inception");
		await Testee.FindIdAsync(original, MediaProvider.RottenTomatoes);

		var found = await Testee.FindIdAsync(remake, MediaProvider.RottenTomatoes);

		await Assert.That(found).IsSome().And.IsEqualTo(RottenTomatoesId.From("m/inception", MediaType.Movie));
		var stored = await host.Resolve<IMediaRepository>().GetAsync([original, remake]);
		await Assert.That(stored.Select(media => media.Identity.Id)).IsEquivalentTo([original, remake]);
		await Assert
			.That(stored.SelectMany(media => media.Cached.Map(cached => cached.Details.PageLinks.Select(link => $"{link}")).GetValueOr([])))
			.IsEquivalentTo(["RottenTomatoes:Movie:m/inception", "RottenTomatoes:Movie:m/inception"]);
	}

	[Test]
	public async Task FindIdAsync_ForAPageLink_StoresItAsALinkAndNotAsAnId()
	{
		var inception = await host.Movie(Inception).SeedWithDetailsAsync("Inception", Fetched);
		host.WikidataHolds(ImdbTitle, "m/inception");

		await Testee.FindIdAsync(inception, MediaProvider.RottenTomatoes);

		host.ScopedDbContext.ChangeTracker.Clear();
		await Assert.That(await host.ScopedDbContext.ExternalMediaIds.AnyAsync(row => row.Source == MediaProvider.RottenTomatoes)).IsFalse();
		await Assert.That(await host.ScopedDbContext.Set<MediaPageLinkRow>().AnyAsync(row => row.Path == "m/inception")).IsTrue();
	}

	[Test]
	public async Task FindIdAsync_ForTwoSitesWikidataAnswersAtOnce_AsksWikidataOnce()
	{
		var inception = await host.Movie(Inception).SeedWithDetailsAsync("Inception", Fetched);
		host.WikidataHoldsPages(ImdbTitle, "m/inception", "movie/inception");

		await Testee.FindIdAsync(inception, MediaProvider.RottenTomatoes);
		var metacritic = await Testee.FindIdAsync(inception, MediaProvider.Metacritic);

		await Assert.That(metacritic).IsSome().And.IsEqualTo(MetacriticId.From("inception", MediaType.Movie));
		await Assert.That(host.WikidataAsked(ImdbTitle)).IsEqualTo(1);
	}

	[Test]
	public async Task FindIdAsync_ForATitleWithoutDetails_FindsThePageButDoesNotKeepIt()
	{
		var inception = await host.Movie(Inception).SeedAsync();
		host.WikidataHolds(ImdbTitle, "m/inception");

		var first = await Testee.FindIdAsync(inception, MediaProvider.RottenTomatoes);
		await Testee.FindIdAsync(inception, MediaProvider.RottenTomatoes);

		await Assert.That(first).IsSome();
		await Assert.That(host.WikidataAsked(ImdbTitle)).IsEqualTo(2);
	}

	[Test]
	public async Task FindIdAsync_ForATitleKnownOnlyByItsImdbId_FindsItsTmdbId()
	{
		var inception = await host.Movie(new MediaSpec("Inception", MediaType.Movie, [Imdb(ImdbTitle)])).SeedAsync();
		await host.TmdbFinds(Inception);

		var found = await Testee.FindIdAsync(inception, MediaProvider.Tmdb);

		await Assert.That(found).IsSome().And.IsEqualTo(TmdbId.From("27205", MediaType.Movie));
	}

	[Test]
	public async Task FindIdAsync_ForATitleKnownOnlyByItsTmdbId_TakesTheImdbIdFromTmdbFirst()
	{
		var inception = await host.Movie(new MediaSpec("Inception", MediaType.Movie, [Tmdb("27205")])).SeedAsync();
		await host.TmdbKnowsTheExternalIdsOf(Inception);
		host.WikidataHolds(ImdbTitle, "m/inception");

		var found = await Testee.FindIdAsync(inception, MediaProvider.RottenTomatoes);

		await Assert.That(found).IsSome().And.IsEqualTo(RottenTomatoesId.From("m/inception", MediaType.Movie));
		var stored = await host.Resolve<IMediaRepository>().GetAsync([inception]);
		await Assert.That(stored.Single().Identity.Keys.OfProvider(MediaProvider.Imdb).Map(key => key.Value)).IsSome().And.IsEqualTo(ImdbTitle);
	}

	[Test]
	public async Task GetIdsAsync_ReturnsOnlyTheIdsATitleAlreadyHolds()
	{
		var inception = await host.Movie(Inception).SeedAsync();
		host.WikidataHolds(ImdbTitle, "m/inception");

		var imdb = await Testee.GetIdsAsync([inception], MediaProvider.Imdb);
		var rottenTomatoes = await Testee.GetIdsAsync([inception], MediaProvider.RottenTomatoes);

		await Assert.That(imdb[inception]).IsEqualTo(ImdbId.From(ImdbTitle, MediaType.Movie));
		await Assert.That(rottenTomatoes).IsEmpty();
		await Assert.That(host.WikidataAsked(ImdbTitle)).IsEqualTo(0);
	}
}
