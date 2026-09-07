using Contracts.Blacklist;
using Domain.Media;
using Domain.Providers;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Blacklist;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class BlacklistBehaviorTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Adding_a_movie_to_the_blacklist_persists_the_entry()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).SeedAsync();

		await host.Resolve<IBlacklistService>().AddToBacklistAsync(fightClub);

		var entries = await host.Resolve<IBlacklistRepository>().GetBlacklistedEntriesAsync();
		await Assert.That(entries.Select(entry => entry.MediaId)).IsEquivalentTo([fightClub]);
	}

	[Test]
	public async Task Adding_a_movie_already_on_the_blacklist_is_idempotent()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).OnBlacklist();

		await host.Resolve<IBlacklistService>().AddToBacklistAsync(fightClub);

		var entries = await host.Resolve<IBlacklistRepository>().GetBlacklistedEntriesAsync();
		await Assert.That(entries.Select(entry => entry.MediaId)).IsEquivalentTo([fightClub]);
	}

	[Test]
	public async Task Removing_a_movie_from_the_blacklist_deletes_the_entry()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).OnBlacklist();

		await host.Resolve<IBlacklistService>().RemoveFromBacklistAsync(fightClub);

		var entries = await host.Resolve<IBlacklistRepository>().GetBlacklistedEntriesAsync();
		await Assert.That(entries).IsEmpty();
	}

	[Test]
	public async Task Getting_blacklist_ids_returns_ids_of_all_blacklisted_items()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).OnBlacklist();
		var inception = await host.Movie(TestMedia.Inception).OnBlacklist();

		var ids = await host.Resolve<IBlacklistService>().GetBlacklistIdsAsync();

		await Assert.That(ids).Contains(fightClub);
		await Assert.That(ids).Contains(inception);
	}

	[Test]
	public async Task Getting_blacklist_items_enriches_entries_with_metadata_and_rating()
	{
		await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb(rating: 8.4, votes: 23000).OnBlacklist();

		var items = await host.Resolve<IBlacklistService>().GetBlacklistItemsAsync().ToListAsync();

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
	public async Task Adding_a_media_that_is_not_in_the_library_is_rejected()
	{
		await Assert.That(async () => await host.Resolve<IBlacklistService>().AddToBacklistAsync(MediaId.From(Guid.NewGuid()))).Throws<UnknownMediaException>();
	}
}
