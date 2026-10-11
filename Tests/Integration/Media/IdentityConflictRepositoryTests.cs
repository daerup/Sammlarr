using Contracts.Media;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class IdentityConflictRepositoryTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Now = new(2026, 10, 11, 3, 0, 0, TimeSpan.Zero);

	private IIdentityConflicts Testee => host.Resolve<IIdentityConflicts>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task SaveAsync_AfterAnUnansweredAsk_KeepsTheAttemptAndWhenToAskAgain()
	{
		var note = await NoteAsync();

		var asked = note.Asked(new LastAsk(RemoteService.Mdblist, AskOutcome.Unavailable, Now));
		await Testee.SaveAsync(asked);

		await Assert.That((await Testee.AllAsync()).Single()).IsEqualTo(asked);
		await Assert.That(await Testee.DueAsync(Now)).IsEmpty();
		await Assert.That(await Testee.DueAsync(Now.AddDays(1))).HasSingleItem();
	}

	[Test]
	public async Task SaveAsync_ForEveryWayAPairIsKeptApart_ReadsTheReasonBack()
	{
		var note = await NoteAsync();
		DistinctReason[] reasons =
		[
			new Contradiction(MediaProvider.Tmdb),
			new TypesDiffer(),
			new AnsweredElsewhere(RemoteService.Mdblist, TmdbId.From("196867", MediaType.Movie)),
			new KeptApartByHand(UserId.From(SammlarrTestHost.SeededAdmin.Id)),
		];

		foreach (var reason in reasons)
		{
			var kept = note with { State = new Distinct(reason, Now) };
			await Testee.SaveAsync(kept);

			await Assert.That((await Testee.AllAsync()).Single()).IsEqualTo(kept);
			await Assert.That(await Testee.DueAsync(Now.AddYears(1))).IsEmpty();
		}
	}

	[Test]
	public async Task AllAsync_AfterOneOfThePairsRowsIsDeleted_HoldsNoNoteForIt()
	{
		var note = await NoteAsync();

		await using var ctx = await host.ContextFactory.CreateDbContextAsync();
		await ctx.Media.Where(media => media.Id == note.Other.Value).ExecuteDeleteAsync();

		await Assert.That(await Testee.AllAsync()).IsEmpty();
	}

	private async Task<IdentityConflict> NoteAsync()
	{
		host.SetCurrentTime(Now);
		await host.SeedMediaAsync(TmdbId.From("15739", MediaType.Movie));
		await host.SeedMediaAsync(ImdbId.From("tt0083564", MediaType.Movie));
		await host.Resolve<IIdentityClaims>()
			.ClaimAsync(new IdentityClaim([TmdbId.From("15739", MediaType.Movie), ImdbId.From("tt0083564", MediaType.Movie)], new None(), [RemoteService.Trakt]));
		return (await Testee.AllAsync()).Single();
	}
}
