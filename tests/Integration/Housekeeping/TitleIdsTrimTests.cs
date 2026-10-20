using Contracts.Housekeeping;
using Contracts.Media;
using Domain.Media;
using Harness;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Housekeeping;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class TitleIdsTrimTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Now = new(2026, 10, 11, 3, 0, 0, TimeSpan.Zero);

	private IHousekeepingStep Testee => host.Step(HousekeepingStep.TitleIds);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task RunAsync_ForATitleWhoseDetailsWentOver185DaysAgo_RemovesItWithItsIds()
	{
		var forgotten = await host.Movie(TestMedia.Inception).SeedAsync();
		var recentlyTrimmed = await host.Movie(TestMedia.FightClub).SeedAsync();
		await TrimmedAsync(forgotten, Now.AddDays(-186));
		await TrimmedAsync(recentlyTrimmed, Now.AddDays(-184));

		var removed = await Testee.RunAsync(Now);

		await using var ctx = await host.ContextFactory.CreateDbContextAsync();
		await Assert.That(removed).IsEqualTo(1);
		await Assert.That(await ctx.Media.Select(media => media.Id).ToListAsync()).IsEquivalentTo([recentlyTrimmed.Value]);
		await Assert.That(await ctx.ExternalMediaIds.AnyAsync(id => id.Media.Id == forgotten.Value)).IsFalse();
	}

	[Test]
	public async Task RunAsync_ForAShowAPartLinksTo_RemovesTheLinkWithIt()
	{
		var forgotten = await host.SeedMediaAsync(TvdbId.From("431162", MediaType.Show));
		await host.Resolve<IShowParts>().AddAsync([AniListId.From("185660", MediaType.Show)], Some.Of(forgotten), new ShowPartPlace(new None(), new None()));
		await host.AddedAsync(forgotten, Now.AddDays(-366));

		await Testee.RunAsync(Now);

		await using var ctx = await host.ContextFactory.CreateDbContextAsync();
		await Assert.That(await ctx.ShowParts.AnyAsync()).IsFalse();
	}

	[Test]
	public async Task RunAsync_ForATitleThatNeverHadDetails_RemovesItOnlyAfterAYear()
	{
		var old = await host.Movie(TestMedia.Inception).SeedAsync();
		var young = await host.Movie(TestMedia.FightClub).SeedAsync();
		await host.AddedAsync(old, Now.AddDays(-366));
		await host.AddedAsync(young, Now.AddDays(-364));

		await Testee.RunAsync(Now);

		await using var ctx = await host.ContextFactory.CreateDbContextAsync();
		await Assert.That(await ctx.Media.Select(media => media.Id).ToListAsync()).IsEquivalentTo([young.Value]);
	}

	[Test]
	public async Task RunAsync_ForAYearOldTitleWhoseDetailsWereClearedByHand_KeepsItsIds()
	{
		var cleared = await host.Movie(TestMedia.Inception).SeedWithDetailsAsync("Inception", Now.AddDays(-10));
		await host.AddedAsync(cleared, Now.AddDays(-400));
		host.SetCurrentTime(Now.AddDays(-1));
		await host.Resolve<IMediaCachePurger>().PurgeDetailsAsync();

		var removed = await Testee.RunAsync(Now);

		await using var ctx = await host.ContextFactory.CreateDbContextAsync();
		await Assert.That(removed).IsEqualTo(0);
		await Assert.That(await ctx.ExternalMediaIds.AnyAsync(id => id.Media.Id == cleared.Value)).IsTrue();
	}

	[Test]
	public async Task RunAsync_ForAYearOldTitleWhoseDetailsWereInvalidated_KeepsItsIds()
	{
		var invalidated = await host.Movie(TestMedia.Inception).SeedWithDetailsAsync("Inception", Now.AddDays(-10));
		await host.AddedAsync(invalidated, Now.AddDays(-400));
		host.SetCurrentTime(Now.AddDays(-1));
		await host.Resolve<IMediaService>().InvalidateAsync([invalidated]);

		var removed = await Testee.RunAsync(Now);

		await using var ctx = await host.ContextFactory.CreateDbContextAsync();
		await Assert.That(removed).IsEqualTo(0);
		await Assert.That(await ctx.ExternalMediaIds.AnyAsync(id => id.Media.Id == invalidated.Value)).IsTrue();
	}

	[Test]
	public async Task RunAsync_ForATitleARecentRunListed_KeepsIt()
	{
		var listed = await host.Movie(TestMedia.Inception).SeedAsync();
		await host.AddedAsync(listed, Now.AddDays(-400));
		await host.SeedRunAsync(await host.SeedFeedAsync(), Now.AddDays(-89), listed.Value);

		var removed = await Testee.RunAsync(Now);

		await Assert.That(removed).IsEqualTo(0);
	}

	[Test]
	public async Task RunAsync_ForABlocklistedOrDetailedTitle_KeepsIt()
	{
		var blocked = await host.Movie(TestMedia.Inception).OnBlocklist();
		var detailed = await host.Movie(TestMedia.FightClub).SeedWithDetailsAsync("Fight Club", Now.AddDays(-400));
		await TrimmedAsync(blocked, Now.AddDays(-300));
		await TrimmedAsync(detailed, Now.AddDays(-300));

		var removed = await Testee.RunAsync(Now);

		await Assert.That(removed).IsEqualTo(0);
	}

	private async Task TrimmedAsync(MediaId id, DateTimeOffset at)
	{
		await using var ctx = await host.ContextFactory.CreateDbContextAsync();
		await ctx
			.Media.Where(media => media.Id == id.Value)
			.ExecuteUpdateAsync(setters => setters.SetProperty(media => media.DetailsTrimmedAt, (DateTimeOffset?)at));
	}
}
