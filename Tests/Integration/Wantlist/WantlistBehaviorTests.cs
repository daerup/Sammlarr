using Contracts.Wantlist;
using DB.Data.Wantlist;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Wantlist;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class WantlistBehaviorTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly WantlistItemProvenance Manual = new(new ManualOrigin(ManualSource.Unknown));
	private static readonly WantlistItemProvenance Remote = new(new SinkOrigin(SinkKind.Plex));

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task AddToWantlistAsync_WithAManualOrigin_PersistsTheEntry()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();

		await host.Resolve<IWantlistService>().AddToWantlistAsync([inception], Manual);

		var entries = await host.ScopedDbContext.Wantlist.AsNoTracking().ToListAsync();
		await Assert.That(entries).Count().IsEqualTo(1);
		await Assert.That(entries[0].Origin).IsEqualTo(WantlistItemOrigin.Manual);
	}

	[Test]
	public async Task AddToWantlistAsync_WhenTheEntryAlreadyExists_KeepsItsOriginalOrigin()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWantlist(origin: Remote);

		await host.Resolve<IWantlistService>().AddToWantlistAsync([inception], Manual);

		var entries = await host.ScopedDbContext.Wantlist.AsNoTracking().ToListAsync();
		await Assert.That(entries).Count().IsEqualTo(1);
		await Assert.That(entries[0].Origin).IsEqualTo(WantlistItemOrigin.Sink);
	}

	[Test]
	public async Task RemoveFromWantlistAsync_WhenAnEntryIsRemoved_HardDeletesIt()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWantlist(origin: Manual);

		await host.Resolve<IWantlistService>().RemoveFromWantlistAsync([inception]);

		await Assert.That(await host.ScopedDbContext.Wantlist.AsNoTracking().CountAsync()).IsEqualTo(0);
	}

	[Test]
	public async Task GetIdsAsync_WhenItemsAreWantlisted_ReturnsAllOfTheirIds()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).OnWantlist(origin: Remote);
		var inception = await host.Movie(TestMedia.Inception).OnWantlist(origin: Remote);

		var ids = await host.Resolve<IWantlistService>().GetWantlistIdsAsync();

		await Assert.That(ids).Contains(fightClub);
		await Assert.That(ids).Contains(inception);
	}

	[Test]
	public async Task AddToWantlistAsync_WhenTheSinkIdIsMissing_IsRejected()
	{
		var solaris = await host.Movie("Solaris", tmdb: "11321").SeedAsync();

		await Assert.That(async () => await host.Resolve<IWantlistService>().AddToWantlistAsync([solaris], Manual)).Throws<MissingSinkIdException>();
	}

	[Test]
	public async Task RemoveFromWantlistAsync_WhenTheSinkIdIsMissing_IsRejected()
	{
		var solaris = await host.Movie("Solaris", tmdb: "11321").OnWantlist(origin: Manual);

		await Assert.That(async () => await host.Resolve<IWantlistService>().RemoveFromWantlistAsync([solaris])).Throws<MissingSinkIdException>();
	}

	[Test]
	public async Task GetItemsAsync_WhenEntriesExist_EnrichesThemWithMetadataAndRating()
	{
		await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb(rating: 8.4, votes: 23000).OnWantlist(origin: Remote);

		var items = await host.Resolve<IWantlistService>().GetWantlistItemsAsync(WantlistQueryOptions.Default).ToListAsync();

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
	public async Task GetItemsAsync_WhenRemovedAreRequested_IncludesPendingRemovals()
	{
		var service = host.Resolve<IWantlistService>();
		var removed = await host.Movie(TestMedia.Inception).ServedByPlex().ServedByTmdb().OnWantlist(origin: Manual);
		var active = await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb().OnWantlist(origin: Manual);

		await service.RemoveFromWantlistAsync([removed]);

		var items = await service.GetWantlistItemsAsync(WantlistQueryOptions.WithRemoved).ToListAsync();

		await Assert.That(items.Select(item => item.Media.Id)).Contains(active);
		await Assert.That(items.Select(item => item.Media.Id)).Contains(removed);
	}

	[Test]
	public async Task GetItemsAsync_ByDefault_ExcludesPendingRemovals()
	{
		var service = host.Resolve<IWantlistService>();
		var removed = await host.Movie(TestMedia.Inception).ServedByPlex().ServedByTmdb().OnWantlist(origin: Manual);
		var active = await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb().OnWantlist(origin: Manual);

		await service.RemoveFromWantlistAsync([removed]);

		var items = await service.GetWantlistItemsAsync(WantlistQueryOptions.Default).ToListAsync();

		await Assert.That(items.Select(item => item.Media.Id)).Contains(active);
		await Assert.That(items.Select(item => item.Media.Id)).DoesNotContain(removed);
	}
}
