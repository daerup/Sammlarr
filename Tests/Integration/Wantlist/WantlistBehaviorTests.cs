using Contracts.Media;
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
	public async Task AddAsync_WithAManualOrigin_PersistsTheItem()
	{
		var inception = await host.Movie(TestMedia.Inception).SeedAsync();

		await host.Resolve<IWantlistService>().AddAsync([inception], Manual);

		var rows = await host.ScopedDbContext.Wantlist.AsNoTracking().ToListAsync();
		await Assert.That(rows).Count().IsEqualTo(1);
		await Assert.That(rows[0].Origin).IsEqualTo(WantlistItemOrigin.Manual);
	}

	[Test]
	public async Task AddAsync_WhenTheTitleIsAlreadyWantlisted_KeepsItsOriginalOrigin()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWantlist(origin: Remote);

		await host.Resolve<IWantlistService>().AddAsync([inception], Manual);

		var rows = await host.ScopedDbContext.Wantlist.AsNoTracking().ToListAsync();
		await Assert.That(rows).Count().IsEqualTo(1);
		await Assert.That(rows[0].Origin).IsEqualTo(WantlistItemOrigin.Sink);
	}

	[Test]
	public async Task RemoveAsync_ForAWantlistedTitle_HardDeletesItsRow()
	{
		var inception = await host.Movie(TestMedia.Inception).OnWantlist(origin: Manual);

		await host.Resolve<IWantlistService>().RemoveAsync([inception]);

		await Assert.That(await host.ScopedDbContext.Wantlist.AsNoTracking().CountAsync()).IsEqualTo(0);
	}

	[Test]
	public async Task GetAsync_WhenItemsAreWantlisted_HoldsAllOfTheirIds()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).OnWantlist(origin: Remote);
		var inception = await host.Movie(TestMedia.Inception).OnWantlist(origin: Remote);

		var wantlist = await host.Resolve<IWantlistService>().GetAsync();

		await Assert.That(wantlist.Ids).Contains(fightClub);
		await Assert.That(wantlist.Ids).Contains(inception);
	}

	[Test]
	public async Task AddAsync_WhenTheSinkIdIsMissing_IsRejected()
	{
		var solaris = await host.Movie("Solaris", tmdb: "11321").SeedAsync();

		await Assert.That(async () => await host.Resolve<IWantlistService>().AddAsync([solaris], Manual)).Throws<MissingSinkIdException>();
	}

	[Test]
	public async Task RemoveAsync_WhenTheSinkIdIsMissing_IsRejected()
	{
		var solaris = await host.Movie("Solaris", tmdb: "11321").OnWantlist(origin: Manual);

		await Assert.That(async () => await host.Resolve<IWantlistService>().RemoveAsync([solaris])).Throws<MissingSinkIdException>();
	}

	[Test]
	public async Task GetDetailsAsync_ForWantlistItems_EnrichesThemWithDetailsAndRating()
	{
		await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb(rating: 8.4, votes: 23000).OnWantlist(origin: Remote);
		var wantlist = await host.Resolve<IWantlistService>().GetAsync();

		var items = await host.Resolve<IMediaService>().GetDetailsAsync(wantlist.Items).ToListAsync();

		await Assert.That(items).Count().IsEqualTo(1);
		await Assert
			.That(items[0].Media)
			.Multiple(async movie =>
			{
				await Assert.That(movie.Details.Title).IsEqualTo("Fight Club");
				await Assert.That(movie.Details.PosterUrl).IsNotNullOrEmpty();
				await Assert.That(movie.Details.Rating.Sources.Any(source => source.Source == RatingSource.Tmdb)).IsTrue();
			});
	}

	[Test]
	public async Task RemoveAsync_ReturnsTheRemovedIds_AndTakesThemOutOfTheSnapshot()
	{
		var service = host.Resolve<IWantlistService>();
		var removed = await host.Movie(TestMedia.Inception).ServedByPlex().ServedByTmdb().OnWantlist(origin: Manual);
		var active = await host.Movie(TestMedia.FightClub).ServedByPlex().ServedByTmdb().OnWantlist(origin: Manual);

		var removedIds = await service.RemoveAsync([removed]);

		var wantlist = await service.GetAsync();
		await Assert.That(removedIds).IsEquivalentTo([removed]);
		await Assert.That(wantlist.Contains(active)).IsTrue();
		await Assert.That(wantlist.Contains(removed)).IsFalse();
	}
}
