using Contracts.Media.Search;
using Domain.Media;
using Harness;
using Integration.Tests.Assertions;
using TestInfrastructure;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MediaSearchTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task A_known_movie_is_returned_with_its_reconciled_identity()
	{
		await host.Movie(TestMedia.FightClub).SeedAsync();
		await host.PlexSearchReturns("Fight Club", TestMedia.FightClub);

		var results = await Search("Fight Club", MediaType.Movie);

		await Assert.That(results).Count().IsEqualTo(1);
		await AssertResolvedAs(results[0], TestMedia.FightClub);
	}

	[Test]
	public async Task A_search_with_no_matches_returns_no_results()
	{
		await host.PlexFindsNothing("Nonexistent");

		await Assert.That(await Search("Nonexistent", MediaType.Movie)).IsEmpty();
	}

	[Test]
	public async Task A_search_returns_both_movies_and_shows()
	{
		await host.Movie(TestMedia.FightClub).SeedAsync();
		await host.Show(TestMedia.BreakingBad).SeedAsync();
		await host.PlexSearchReturns("thriller", TestMedia.FightClub, TestMedia.BreakingBad);

		var results = await Search("thriller", MediaType.Movie, MediaType.Show);

		await Assert.That(results).Count().IsEqualTo(2);
		await AssertResolvedAs(results.Single(item => item.Title == "Fight Club"), TestMedia.FightClub);
		await AssertResolvedAs(results.Single(item => item.Title == "Breaking Bad"), TestMedia.BreakingBad);
	}

	private async Task<IReadOnlyList<MediaLookup>> Search(string query, params MediaType[] types) =>
		await host.Resolve<IMediaSearchService>().SearchAsync(query, types);

	private static async Task AssertResolvedAs(MediaLookup item, MediaSpec spec)
	{
		await Assert.That(item).IsOfUnionType<MediaLookup, IdentifiedMedia>();
		await Assert.That(item.Title).IsEqualTo(spec.Title);
		await Assert.That(item.MediaType).IsEqualTo(spec.Type);
		foreach (var id in spec.Ids)
		{
			await Assert.That(item.Externals.Any(external => external.Source == id.Provider && external.Value == id.Value)).IsTrue();
		}
	}
}
