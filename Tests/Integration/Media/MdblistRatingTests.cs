using Contracts.Media;
using Contracts.Ratings;
using Domain.Media;
using Domain.Providers;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MdblistRatingTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string FightClub = """
		[{"ids":{"imdb":"tt0137523","tmdb":550,"tvdb":null,"trakt":432,"mal":null,"mdblist":"a5as"},"ratings":[
		{"source":"imdb","value":8.8,"score":88,"votes":2668786,"url":143},
		{"source":"metacritic","value":67,"score":67,"votes":36,"url":"/fight-club"},
		{"source":"tomatoes","value":81,"score":81,"votes":251,"url":"/m/fight_club"},
		{"source":"popcorn","value":96,"score":96,"votes":73870,"url":"/m/fight_club"},
		{"source":"letterboxd","value":4.3,"score":86,"votes":6143532,"url":"/film/fight-club/"},
		{"source":"rogerebert","value":2.0,"score":null,"votes":null,"url":"fight-club-1999"}]}]
		""";

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task GetDetailsAsync_WithOnlyAnMdblistKey_RatesTheTitleWithEverySourceMdblistSends()
	{
		await host.WithMdblistAccount("valid-mdblist-key");
		host.HttpStub.RespondWithJsonForUrl($"{TestUrls.MdblistApi}/imdb/movie", FightClub);
		var fightClub = await host.SeedMediaAsync(ImdbId.From("tt0137523", MediaType.Movie));

		var loaded = await host.Resolve<IMediaService>().GetDetailsAsync([fightClub]).ToListAsync();

		var matched = await Assert.That(loaded.Single()).IsSuccess();
		await Assert
			.That(matched!.Details.Ratings.All.Select(rating => rating.Source))
			.IsEquivalentTo([
				RatingSource.Imdb,
				RatingSource.RottenTomatoesCritics,
				RatingSource.RottenTomatoesAudience,
				RatingSource.Metacritic,
				RatingSource.Letterboxd,
			]);
	}

	[Test]
	public async Task GetDetailsAsync_WithOnlyAnMdblistKey_StoresItsRatingPagesAsPageLinks()
	{
		await host.WithMdblistAccount("valid-mdblist-key");
		host.HttpStub.RespondWithJsonForUrl($"{TestUrls.MdblistApi}/imdb/movie", FightClub);
		var fightClub = await host.SeedMediaAsync(ImdbId.From("tt0137523", MediaType.Movie));

		await host.Resolve<IMediaService>().GetDetailsAsync([fightClub]).ToListAsync();

		var stored = (await host.Resolve<IMediaRepository>().GetAsync([fightClub])).Single();
		await Assert
			.That(stored.Cached.Map(cached => cached.Details.PageLinks.Select(link => $"{link}")).GetValueOr([]))
			.IsEquivalentTo(["RottenTomatoes:Movie:m/fight_club", "Metacritic:Movie:fight-club", "Letterboxd:Movie:fight-club"]);
		await Assert.That(stored.Identity.Keys.Select(key => key.Source)).DoesNotContain(MediaProvider.Metacritic);
	}

	[Test]
	public async Task GetDetailsAsync_WithOnlyAnMdblistKey_CountsMetacriticAndLetterboxdInTheSammlarrRating()
	{
		await host.WithMdblistAccount("valid-mdblist-key");
		host.HttpStub.RespondWithJsonForUrl($"{TestUrls.MdblistApi}/imdb/movie", FightClub);
		var fightClub = await host.SeedMediaAsync(ImdbId.From("tt0137523", MediaType.Movie));

		var loaded = await host.Resolve<IMediaService>().GetDetailsAsync([fightClub]).ToListAsync();
		var available = await host.Resolve<IRatingService>().GetAvailableAsync();

		var matched = await Assert.That(loaded.Single()).IsSuccess();
		var counted = new RatingStrategy(AveragedRating.Everything)
			.Resolve(matched!.Details.Ratings, available)
			.Map(rating => rating.Counted.Select(part => part.Source));
		await Assert.That(counted.GetValueOr([])).Contains(RatingSource.Metacritic).And.Contains(RatingSource.Letterboxd);
	}
}
