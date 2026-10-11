using Contracts.Blocklist;
using Contracts.Media;
using Domain.Blocklist;
using Domain.Media;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Blocklist;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class BlocklistBehaviorTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task AddAsync_WhenAMovieIsBlocklisted_PersistsTheItem()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).SeedAsync();

		await host.Resolve<IBlocklistService>().AddAsync([fightClub]);

		var items = await host.Resolve<IBlocklistRepository>().GetAllAsync();
		await Assert.That(items.Select(item => item.MediaId)).IsEquivalentTo([fightClub]);
	}

	[Test]
	public async Task AddAsync_WhenTheMovieIsAlreadyBlocklisted_IsIdempotent()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).OnBlocklist();

		await host.Resolve<IBlocklistService>().AddAsync([fightClub]);

		var items = await host.Resolve<IBlocklistRepository>().GetAllAsync();
		await Assert.That(items.Select(item => item.MediaId)).IsEquivalentTo([fightClub]);
	}

	[Test]
	public async Task RemoveAsync_WhenAMovieIsRemoved_DeletesTheItem()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).OnBlocklist();

		await host.Resolve<IBlocklistService>().RemoveAsync([fightClub]);

		var items = await host.Resolve<IBlocklistRepository>().GetAllAsync();
		await Assert.That(items).IsEmpty();
	}

	[Test]
	public async Task GetAsync_WhenTitlesAreBlocklisted_HoldsAllOfTheirIds()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).OnBlocklist();
		var inception = await host.Movie(TestMedia.Inception).OnBlocklist();

		var blocklist = await host.Resolve<IBlocklistService>().GetAsync();

		await Assert.That(blocklist.Ids).IsEquivalentTo([fightClub, inception]);
	}

	[Test]
	public async Task GetDetailsAsync_ForBlocklistItems_EnrichesThemWithDetailsAndRating()
	{
		await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb(rating: 8.4, votes: 23000).OnBlocklist();
		var blocklist = await host.Resolve<IBlocklistService>().GetAsync();

		var items = await host.Resolve<IMediaService>().GetDetailsAsync(blocklist.Items, FieldNeeds.Everything).ToListAsync();

		await Assert.That(items).Count().IsEqualTo(1);
		await Assert
			.That(items[0].Media)
			.Multiple(async movie =>
			{
				await Assert.That(movie.Details.Title).IsEqualTo("Fight Club");
				await Assert.That(movie.Details.PosterUrl).IsNotNullOrEmpty();
				await Assert.That(movie.Details.Ratings.All.Any(rating => rating.Source == RatingSource.Tmdb)).IsTrue();
			});
	}

	[Test]
	public async Task AddAsync_ForANewTitle_AnnouncesItAsAdded()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).SeedAsync();
		var heard = host.Heard();

		await host.Resolve<IBlocklistService>().AddAsync([fightClub]);

		var changed = heard.OfType<BlocklistChangedEvent>().Single();
		await Assert.That(changed.Added).IsEquivalentTo([fightClub]);
		await Assert.That(changed.Removed).IsEmpty();
	}

	[Test]
	public async Task AddAsync_WhenTheTitleIsAlreadyBlocklisted_AnnouncesNothing()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).OnBlocklist();
		var heard = host.Heard();

		await host.Resolve<IBlocklistService>().AddAsync([fightClub]);

		await Assert.That(heard.OfType<BlocklistChangedEvent>()).IsEmpty();
	}

	[Test]
	public async Task RemoveAsync_ForABlocklistedTitle_AnnouncesItAsRemoved()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).OnBlocklist();
		var heard = host.Heard();

		await host.Resolve<IBlocklistService>().RemoveAsync([fightClub]);

		var changed = heard.OfType<BlocklistChangedEvent>().Single();
		await Assert.That(changed.Removed).IsEquivalentTo([fightClub]);
		await Assert.That(changed.Added).IsEmpty();
	}

	[Test]
	public async Task RemoveAsync_WhenTheTitleIsNotBlocklisted_AnnouncesNothing()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).SeedAsync();
		var heard = host.Heard();

		await host.Resolve<IBlocklistService>().RemoveAsync([fightClub]);

		await Assert.That(heard.OfType<BlocklistChangedEvent>()).IsEmpty();
	}

	[Test]
	public async Task AddAsync_WhenTheMediaIsNotInTheLibrary_IsRejected()
	{
		await Assert.That(async () => await host.Resolve<IBlocklistService>().AddAsync([MediaId.New()])).Throws<UnknownMediaException>();
	}
}
