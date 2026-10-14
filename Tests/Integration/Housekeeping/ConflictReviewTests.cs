using Contracts.Media;
using Contracts.Wantlist;
using Core.Jobs;
using DB.Data.Feed.Run;
using Domain.Feed.Run;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Housekeeping;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class ConflictReviewTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Now = new(2026, 10, 12, 21, 0, 0, TimeSpan.Zero);

	private IConflictReview Testee => host.Resolve<IConflictReview>();

	private IIdentityConflicts Notes => host.Resolve<IIdentityConflicts>();

	[Before(Test)]
	public void SetClock() => host.SetCurrentTime(Now);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task PairsAsync_ForANotedPair_ReadsBothTitlesAndTheWantlistsAndFeedsThatStillListThem()
	{
		var (held, other) = await host.StoredTwiceAsync("15739", "tt0083564");
		await host.Resolve<IWantlistRepository>().AddAsync([other], new ManualOrigin(ManualSource.Search));
		var feed = await host.SeedFeedAsync();
		await host.SeedRunAsync(feed, Now, other.Value);
		var dropped = await host.SeedRunAsync(await host.SeedFeedAsync(), Now, other.Value);
		await using (var ctx = await host.ContextFactory.CreateDbContextAsync())
		{
			ctx.FeedRunItems.Add(
				new FeedRunItemRow
				{
					Id = Guid.NewGuid(),
					FeedRunId = dropped,
					MediaId = held.Value,
					State = FeedRunItemState.Removed,
				}
			);
			await ctx.SaveChangesAsync();
		}

		var pair = (await Testee.PairsAsync()).Single();

		await Assert.That((pair.Held.Media.Identity.Id, pair.Other.Media.Identity.Id)).IsEqualTo((held, other));
		await Assert.That((pair.Held.Uses, pair.Other.Uses)).IsEqualTo((new TitleUses(0, 0), new TitleUses(1, 2)));
	}

	[Test]
	public async Task ApplyAsync_ForAMerge_MakesThePairOneTitle()
	{
		var (held, other) = await host.StoredTwiceAsync("15739", "tt0083564");
		var note = (await Notes.AllAsync()).Single();

		var refused = await Testee.ApplyAsync([new ReviewChoice(note, ConflictChoice.Merge)]);

		await Assert.That(refused).IsEmpty();
		await Assert.That(await Notes.AllAsync()).IsEmpty();
		await Assert.That(await host.Resolve<IMediaRepository>().GetAsync([held, other])).HasSingleItem();
	}

	[Test]
	public async Task ApplyAsync_ForKeepApartThenMergeAnyway_KeepsThemApartUntilTheMerge()
	{
		await host.StoredTwiceAsync("15739", "tt0083564");
		var note = (await Notes.AllAsync()).Single();

		await Testee.ApplyAsync([new ReviewChoice(note, ConflictChoice.KeepApart)]);
		var kept = (await Notes.AllAsync()).Single();
		await Testee.ApplyAsync([new ReviewChoice(kept, ConflictChoice.Merge)]);

		await Assert
			.That(kept.State)
			.IsOfUnionType<ConflictState, Distinct>()
			.And.IsEqualTo(new Distinct(new KeptApartByHand(UserId.From(SammlarrTestHost.SeededAdmin.Id)), Now));
		await Assert.That(await Notes.AllAsync()).IsEmpty();
	}

	[Test]
	public async Task ApplyAsync_ForLeave_ChangesNothing()
	{
		await host.StoredTwiceAsync("15739", "tt0083564");
		var note = (await Notes.AllAsync()).Single();

		await Testee.ApplyAsync([new ReviewChoice(note, ConflictChoice.Leave)]);

		await Assert.That((await Notes.AllAsync()).Single()).IsEqualTo(note);
	}

	[Test]
	public async Task ApplyAsync_ForAMergeARowNowContradicts_RefusesItAndKeepsThemApart()
	{
		var (_, other) = await host.StoredTwiceAsync("15739", "tt0083564");
		var note = (await Notes.AllAsync()).Single();
		await host.Resolve<IIdentityClaims>().ClaimAsync(new IdentityClaim([TmdbId.From("196867", MediaType.Movie)], Some.Of(other), [RemoteService.Tmdb]));

		var refused = await Testee.ApplyAsync([new ReviewChoice(note, ConflictChoice.Merge)]);

		await Assert.That(refused.Single().Reason).IsEqualTo(new Contradiction(MediaProvider.Tmdb));
		await Assert.That((await Notes.AllAsync()).Single().State).IsOfUnionType<ConflictState, Distinct>();
	}

	[Test]
	public async Task ApplyAsync_WhileAJobHoldsTheTurn_WaitsForIt()
	{
		await host.StoredTwiceAsync("15739", "tt0083564");
		var note = (await Notes.AllAsync()).Single();
		var held = await host.Resolve<JobTurn>().TakeAsync(CancellationToken.None);

		var applying = Testee.ApplyAsync([new ReviewChoice(note, ConflictChoice.Merge)]);
		var waited = (Testee.WaitsForAJob, applying.IsCompleted);
		held.Dispose();
		await applying;

		await Assert.That(waited).IsEqualTo((true, false));
		await Assert.That(await Notes.AllAsync()).IsEmpty();
	}

	[Test]
	public async Task PairsAsync_ForAMember_IsRejected()
	{
		await host.SignInAs("member");

		await Assert.That(async () => await Testee.PairsAsync()).Throws<UnauthorizedAccessException>();
	}

	[Test]
	public async Task ApplyAsync_ForAMember_IsRejected()
	{
		await host.StoredTwiceAsync("15739", "tt0083564");
		var note = (await Notes.AllAsync()).Single();
		await host.SignInAs("member");

		await Assert.That(async () => await Testee.ApplyAsync([new ReviewChoice(note, ConflictChoice.Merge)])).Throws<UnauthorizedAccessException>();
	}
}
