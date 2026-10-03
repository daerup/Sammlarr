using Contracts.Media.Search;
using Domain.Media;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MediaSearchTests(SammlarrTestHost host) : IAsyncDisposable
{
	private IMediaSearchService Testee => host.Resolve<IMediaSearchService>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task SearchAsync_WhenTheMovieIsKnown_ReturnsItWithAReconciledIdentity()
	{
		await host.Movie(TestMedia.FightClub).SeedAsync();
		await host.PlexSearchReturns("Fight Club", TestMedia.FightClub);

		var results = await Testee.SearchAsync("Fight Club", [MediaType.Movie]);

		await Assert.That(results).Count().IsEqualTo(1);
		await AssertResolvedAs(results[0], TestMedia.FightClub);
	}

	[Test]
	public async Task SearchAsync_WhenNothingMatches_ReturnsNoResults()
	{
		await host.PlexFindsNothing("Nonexistent");

		await Assert.That(await Testee.SearchAsync("Nonexistent", [MediaType.Movie])).IsEmpty();
	}

	[Test]
	public async Task SearchAsync_WhenResultsMixTypes_ReturnsMoviesAndShows()
	{
		await host.Movie(TestMedia.FightClub).SeedAsync();
		await host.Show(TestMedia.BreakingBad).SeedAsync();
		await host.PlexSearchReturns("thriller", TestMedia.FightClub, TestMedia.BreakingBad);

		var results = await Testee.SearchAsync("thriller", [MediaType.Movie, MediaType.Show]);

		await Assert.That(results).Count().IsEqualTo(2);
		await AssertResolvedAs(results.Single(item => item.Details.Title == "Fight Club"), TestMedia.FightClub);
		await AssertResolvedAs(results.Single(item => item.Details.Title == "Breaking Bad"), TestMedia.BreakingBad);
	}

	private static async Task AssertResolvedAs(MediaMatchResult item, MediaSpec spec)
	{
		await Assert.That(item).IsOfUnionType<MediaMatchResult, MatchedMedia>();
		await Assert.That(item.Details.Title).IsEqualTo(spec.Title);
		await Assert.That(item.Details.Type).IsEqualTo(spec.Type);
		foreach (var id in spec.Ids)
		{
			await Assert.That(item.Externals.Any(external => external.Source == id.Provider && external.Value == id.Value)).IsTrue();
		}
	}
}
