using Contracts.Media.Metadata;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Browse;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class TmdbRatingTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	[Arguments(8.4, 23000)]
	[Arguments(6.5, 0)]
	[Arguments(0.1, 1)]
	public async Task A_positive_vote_average_delivers_a_rating(double average, int votes)
	{
		await host.Movie(TestMedia.FightClub).ServedByTmdb(rating: average, votes: votes).SeedAsync();

		var ratings = await FetchTmdbRatings(TmdbId.From("550", MediaType.Movie));

		await Assert.That(ratings).Count().IsEqualTo(1);
		await Assert.That(ratings[0].Source).IsEqualTo(RatingProvider.Tmdb);
		await Assert.That(ratings[0].NormalizedValue).IsEqualTo((float)average);
		await Assert.That(ratings[0].VoteCount).IsEqualTo(votes);
	}

	[Test]
	public async Task A_vote_average_of_zero_returns_no_ratings()
	{
		await host.Movie(TestMedia.FightClub).ServedByTmdb(rating: 0, votes: 0).SeedAsync();

		await Assert.That(await FetchTmdbRatings(TmdbId.From("550", MediaType.Movie))).IsEmpty();
	}

	[Test]
	public async Task Missing_vote_data_returns_no_ratings()
	{
		await host.Movie(TestMedia.FightClub).ServedByTmdb().SeedAsync();

		await Assert.That(await FetchTmdbRatings(TmdbId.From("550", MediaType.Movie))).IsEmpty();
	}

	[Test]
	public async Task A_non_tmdb_id_returns_no_ratings() => await Assert.That(await FetchTmdbRatings(ImdbId.From("tt0137523", MediaType.Movie))).IsEmpty();

	[Test]
	public async Task Ratings_are_retrieved_for_a_show()
	{
		await host.Show(TestMedia.BreakingBad).ServedByTmdb(rating: 9.5, votes: 50000).SeedAsync();

		var ratings = await FetchTmdbRatings(TmdbId.From("1396", MediaType.Show));

		await Assert.That(ratings).Count().IsEqualTo(1);
		await Assert.That(ratings[0].NormalizedValue).IsEqualTo(9.5f);
		await Assert.That(ratings[0].VoteCount).IsEqualTo(50000);
	}

	private async Task<IReadOnlyList<MediaRating>> FetchTmdbRatings(IExternalMediaId id) =>
		(await host.ResolveKeyed<IMediaRatingService>(RatingProvider.Tmdb).GetRatingsAsync(id)).GetValueOr([]);
}
