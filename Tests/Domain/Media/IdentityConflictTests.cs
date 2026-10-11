using Domain.Media;
using Domain.Providers;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Domain.Tests.Media;

public class IdentityConflictTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 11, 3, 0, 0, TimeSpan.Zero);

	private static readonly IdentityConflict Fresh = new(
		MediaId.From(new Guid(1, 0, 0, new byte[8])),
		MediaId.From(new Guid(2, 0, 0, new byte[8])),
		TmdbId.From("15739", MediaType.Movie),
		[RemoteService.Trakt],
		Now,
		0,
		Now,
		new None(),
		new Undecided()
	);

	[Test]
	public async Task Asked_WithoutAnAnswerFourTimes_WaitsADayThenAWeekThenAMonthEachTime()
	{
		List<TimeSpan> waits = [];
		var conflict = Fresh;
		for (var ask = 0; ask < 4; ask++)
		{
			conflict = conflict.Asked(new LastAsk(RemoteService.Mdblist, AskOutcome.Unavailable, Now));
			waits.Add(conflict.AskAfter - Now);
		}

		await Assert
			.That(waits)
			.IsEquivalentTo([TimeSpan.FromDays(1), TimeSpan.FromDays(7), TimeSpan.FromDays(30), TimeSpan.FromDays(30)], CollectionOrdering.Matching);
		await Assert.That(conflict.Attempts).IsEqualTo(4);
		await Assert.That(conflict.LastAsk).IsSome().And.IsEqualTo(new LastAsk(RemoteService.Mdblist, AskOutcome.Unavailable, Now));
	}

	[Test]
	public async Task Contradicts_ForRowsThatEachHoldAnIdOfOneSource_NamesThatSource()
	{
		var contradiction = IdentityConflict.Contradicts(
			[TmdbId.From("812", MediaType.Movie), ImdbId.From("tt0103639", MediaType.Movie)],
			[TmdbId.From("420817", MediaType.Movie)]
		);

		await Assert.That(contradiction).IsSome().And.IsEqualTo<DistinctReason>(new Contradiction(MediaProvider.Tmdb));
	}

	[Test]
	public async Task Contradicts_ForAFilmAndAShow_SaysTheirTypesDiffer()
	{
		var contradiction = IdentityConflict.Contradicts([TmdbId.From("812", MediaType.Movie)], [ImdbId.From("tt0103639", MediaType.Show)]);

		await Assert.That(contradiction).IsSome().And.IsEqualTo<DistinctReason>(new TypesDiffer());
	}

	[Test]
	public async Task Contradicts_ForRowsOfOneTypeWithNoSourceInCommon_FindsNone()
	{
		var contradiction = IdentityConflict.Contradicts([TmdbId.From("693134", MediaType.Movie)], [ImdbId.From("tt15239678", MediaType.Movie)]);

		await Assert.That(contradiction).IsNone();
	}
}
