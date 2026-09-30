using Contracts.Blocklist;
using Domain.Media;
using Domain.Providers;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Blocklist;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class BlocklistBehaviorTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task AddAsync_WhenAMovieIsBlocklisted_PersistsTheEntry()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).SeedAsync();

		await host.Resolve<IBlocklistService>().AddToBlocklistAsync(fightClub);

		var entries = await host.Resolve<IBlocklistRepository>().GetBlocklistedEntriesAsync();
		await Assert.That(entries.Select(entry => entry.MediaId)).IsEquivalentTo([fightClub]);
	}

	[Test]
	public async Task AddAsync_WhenTheMovieIsAlreadyBlocklisted_IsIdempotent()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).OnBlocklist();

		await host.Resolve<IBlocklistService>().AddToBlocklistAsync(fightClub);

		var entries = await host.Resolve<IBlocklistRepository>().GetBlocklistedEntriesAsync();
		await Assert.That(entries.Select(entry => entry.MediaId)).IsEquivalentTo([fightClub]);
	}

	[Test]
	public async Task RemoveAsync_WhenAMovieIsRemoved_DeletesTheEntry()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).OnBlocklist();

		await host.Resolve<IBlocklistService>().RemoveFromBlocklistAsync(fightClub);

		var entries = await host.Resolve<IBlocklistRepository>().GetBlocklistedEntriesAsync();
		await Assert.That(entries).IsEmpty();
	}

	[Test]
	public async Task GetIdsAsync_WhenItemsAreBlocklisted_ReturnsAllOfTheirIds()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).OnBlocklist();
		var inception = await host.Movie(TestMedia.Inception).OnBlocklist();

		var ids = await host.Resolve<IBlocklistService>().GetBlocklistIdsAsync();

		await Assert.That(ids).Contains(fightClub);
		await Assert.That(ids).Contains(inception);
	}

	[Test]
	public async Task GetItemsAsync_WhenEntriesExist_EnrichesThemWithMetadataAndRating()
	{
		await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb(rating: 8.4, votes: 23000).OnBlocklist();

		var items = await host.Resolve<IBlocklistService>().GetBlocklistItemsAsync().ToListAsync();

		await Assert.That(items).Count().IsEqualTo(1);
		await Assert
			.That(items[0].Media)
			.IsOfUnionType<IdentifiedMedia, PartialMovie>()
			.And.Multiple(async movie =>
			{
				await Assert.That(movie.Title).IsEqualTo("Fight Club");
				await Assert.That(movie.PosterUrl).IsNotNullOrEmpty();
				await Assert.That(movie.Rating.Sources.Any(source => source.Source == RatingProvider.Tmdb)).IsTrue();
			});
	}

	[Test]
	public async Task AddAsync_WhenTheMediaIsNotInTheLibrary_IsRejected()
	{
		await Assert.That(async () => await host.Resolve<IBlocklistService>().AddToBlocklistAsync(MediaId.From(Guid.NewGuid()))).Throws<UnknownMediaException>();
	}
}
