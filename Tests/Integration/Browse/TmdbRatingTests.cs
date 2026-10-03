using Contracts.Media;
using Domain.Media;
using Domain.Providers;
using Harness;

namespace Integration.Tests.Browse;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class TmdbRatingTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	[Arguments(8.4, 23000, 23000)]
	[Arguments(6.5, 0, null)]
	[Arguments(0.1, 1, 1)]
	public async Task LookUpAsync_WhenTheVoteAverageIsPositive_DeliversARating(double average, int votes, int? voteCount)
	{
		await host.Movie(TestMedia.FightClub).ServedByTmdb(rating: average, votes: votes).SeedAsync();

		var ratings = await FetchTmdbRatings(TmdbId.From("550", MediaType.Movie));

		await Assert.That(ratings).Count().IsEqualTo(1);
		await Assert.That(ratings[0].Source).IsEqualTo(MediaProvider.Tmdb);
		await Assert.That(ratings[0].NormalizedValue).IsEqualTo((float)average);
		await Assert.That(ratings[0].VoteCount).IsEqualTo(voteCount);
	}

	[Test]
	public async Task LookUpAsync_WhenTheVoteAverageIsZero_ReturnsNone()
	{
		await host.Movie(TestMedia.FightClub).ServedByTmdb(rating: 0, votes: 0).SeedAsync();

		await Assert.That(await FetchTmdbRatings(TmdbId.From("550", MediaType.Movie))).IsEmpty();
	}

	[Test]
	public async Task LookUpAsync_WhenVoteDataIsMissing_ReturnsNone()
	{
		await host.Movie(TestMedia.FightClub).ServedByTmdb().SeedAsync();

		await Assert.That(await FetchTmdbRatings(TmdbId.From("550", MediaType.Movie))).IsEmpty();
	}

	[Test]
	public async Task LookUpAsync_WhenTheIdIsNotTmdb_ReturnsNone() =>
		await Assert.That(await FetchTmdbRatings(ImdbId.From("tt0137523", MediaType.Movie))).IsEmpty();

	[Test]
	public async Task LookUpAsync_ForAShow_RetrievesRatings()
	{
		await host.Show(TestMedia.BreakingBad).ServedByTmdb(rating: 9.5, votes: 50000).SeedAsync();

		var ratings = await FetchTmdbRatings(TmdbId.From("1396", MediaType.Show));

		await Assert.That(ratings).Count().IsEqualTo(1);
		await Assert.That(ratings[0].NormalizedValue).IsEqualTo(9.5f);
		await Assert.That(ratings[0].VoteCount).IsEqualTo(50000);
	}

	private async Task<IReadOnlyList<MediaRating>> FetchTmdbRatings(IExternalMediaId id)
	{
		var found = await host.ResolveKeyed<IDetailsEnricher>(MediaProvider.Tmdb).LookUpAsync(id);
		return found.Map<FoundMedia, IReadOnlyList<MediaRating>>(media => [.. media.Details.Rating.Sources]).GetValueOr([]);
	}
}
