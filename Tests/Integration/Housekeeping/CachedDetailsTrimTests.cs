using Contracts.Housekeeping;
using Domain.Media;
using Domain.Wantlist;
using Harness;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Housekeeping;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class CachedDetailsTrimTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Now = new(2026, 10, 11, 3, 0, 0, TimeSpan.Zero);

	private IHousekeepingStep Testee => host.Step(HousekeepingStep.CachedDetails);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task RunAsync_ForDetailsNobodyOpenedFor194Days_RemovesThemAndStampsTheTitle()
	{
		var unused = await host.Movie(TestMedia.Inception).SeedWithDetailsAsync("Inception", Now.AddDays(-195));
		var opened = await host.Movie(TestMedia.FightClub).SeedWithDetailsAsync("Fight Club", Now.AddDays(-193));

		var removed = await Testee.RunAsync(Now);

		await using var ctx = await host.ContextFactory.CreateDbContextAsync();
		await Assert.That(removed).IsEqualTo(1);
		await Assert.That(await ctx.MediaDetails.Select(details => details.MediaId).ToListAsync()).IsEquivalentTo([opened.Value]);
		await Assert.That(await TrimmedAtAsync(unused)).IsEqualTo(Now);
		await Assert.That(await TrimmedAtAsync(opened)).IsNull();
		await Assert.That(await ctx.ExternalMediaIds.CountAsync(id => id.Media.Id == unused.Value)).IsGreaterThan(0);
	}

	[Test]
	public async Task RunAsync_ForAWantlistedTitleNobodyOpened_KeepsItsDetails()
	{
		var wanted = host.Movie(TestMedia.Interstellar);
		var id = await wanted.SeedWithDetailsAsync("Interstellar", Now.AddDays(-300));
		await wanted.OnWantlist(new WantlistItemProvenance(new ManualOrigin(ManualSource.Unknown)));

		var removed = await Testee.RunAsync(Now);

		await using var ctx = await host.ContextFactory.CreateDbContextAsync();
		await Assert.That(removed).IsEqualTo(0);
		await Assert.That(await ctx.MediaDetails.AnyAsync(details => details.MediaId == id.Value)).IsTrue();
	}

	[Test]
	public async Task RunAsync_ForATitleARecentRunListed_KeepsItsDetails()
	{
		var listed = await host.Movie(TestMedia.Interstellar).SeedWithDetailsAsync("Interstellar", Now.AddDays(-300));
		await host.SeedRunAsync(await host.SeedFeedAsync(), Now.AddDays(-89), listed.Value);

		var removed = await Testee.RunAsync(Now);

		await Assert.That(removed).IsEqualTo(0);
	}

	private async Task<DateTimeOffset?> TrimmedAtAsync(MediaId id)
	{
		await using var ctx = await host.ContextFactory.CreateDbContextAsync();
		return await ctx.Media.Where(media => media.Id == id.Value).Select(media => media.DetailsTrimmedAt).SingleAsync();
	}
}
