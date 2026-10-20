using Contracts.Housekeeping;
using Contracts.Media;
using Domain.Media;
using Harness;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Housekeeping;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class ShowPartsTrimTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Now = new(2026, 10, 11, 3, 0, 0, TimeSpan.Zero);

	private static readonly ShowPartPlace Unplaced = new(new None(), new None());

	private IHousekeepingStep Testee => host.Step(HousekeepingStep.ShowParts);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task RunAsync_ForANoShowAnswerOlderThanThreeDays_RemovesIt()
	{
		await host.NoShowAnsweredAsync(MalId.From("60001", MediaType.Show), Now.AddDays(-3).AddMinutes(-1));

		var removed = await Testee.RunAsync(Now);

		await Assert.That(removed).IsEqualTo(1);
		await Assert.That(await IdsAsync()).IsEmpty();
	}

	[Test]
	public async Task RunAsync_ForANoShowAnswerFromTheLastThreeDays_KeepsIt()
	{
		await host.NoShowAnsweredAsync(MalId.From("60001", MediaType.Show), Now.AddDays(-3).AddMinutes(1));

		var removed = await Testee.RunAsync(Now);

		await Assert.That(removed).IsEqualTo(0);
		await Assert.That(await IdsAsync()).IsEquivalentTo(["60001"]);
	}

	[Test]
	public async Task RunAsync_ForALinkedPart_KeepsIt()
	{
		var show = await host.SeedMediaAsync(TvdbId.From("431162", MediaType.Show));
		host.SetCurrentTime(Now.AddDays(-400));
		await host.Resolve<IShowParts>().AddAsync([AniListId.From("185660", MediaType.Show)], Some.Of(show), Unplaced);

		var removed = await Testee.RunAsync(Now);

		await Assert.That(removed).IsEqualTo(0);
		await Assert.That(await IdsAsync()).IsEquivalentTo(["185660"]);
	}

	private async Task<List<string>> IdsAsync()
	{
		await using var ctx = await host.ContextFactory.CreateDbContextAsync();
		return await ctx.ShowParts.Select(part => part.Id).ToListAsync();
	}
}
