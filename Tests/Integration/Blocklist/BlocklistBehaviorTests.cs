using Contracts.Blocklist;
using Domain.Blocklist;
using Domain.Media;
using Harness;

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
