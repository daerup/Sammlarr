using Contracts;
using Contracts.Usage;
using Domain.Providers;
using Domain.Usage;

namespace UI.Tests.Settings.Usage;

internal static class UsageSamples
{
	internal static readonly DateTimeOffset To = new(2026, 10, 7, 13, 0, 0, TimeSpan.Zero);

	internal static readonly UsageRightNow Quiet = new(new None(), new None(), new None(), new None(), new None());

	internal static UsageTally Tally(long answered, long after = 0, long gaveUp = 0, long failed = 0, long cancelled = 0)
	{
		return new UsageTally(
			new Dictionary<UsageOutcome, long>
			{
				[UsageOutcome.Answered] = answered,
				[UsageOutcome.AnsweredAfter429] = after,
				[UsageOutcome.GaveUpOn429] = gaveUp,
				[UsageOutcome.Failed] = failed,
				[UsageOutcome.Cancelled] = cancelled,
			},
			new Dictionary<int, long> { [100] = answered, [500] = after + failed }
		);
	}

	internal static IDateTimeProvider Clock()
	{
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(To.AddHours(-3));
		return clock.Object;
	}

	private static readonly UsageWindow Window = new(To.AddHours(-24), To, TimeSpan.FromHours(1));

	internal static UsageServiceLine Line(RemoteService service, UsageTally tally, UsageRightNow? rightNow = null)
	{
		return new(service, tally, rightNow ?? Quiet, Slots(tally));
	}

	internal static UsagePeriod Period(IReadOnlyList<UsageServiceLine> services, IReadOnlyList<UsageCauseLine> causes)
	{
		var tally = UsageTally.Sum(services.Select(line => line.Tally));
		return new UsagePeriod(Window, tally, Slots(tally), services, causes);
	}

	private static IReadOnlyList<UsageSlot> Slots(UsageTally lastHour)
	{
		return [.. Enumerable.Range(0, 24).Select(slot => new UsageSlot(Window.SlotAt(slot), slot == 23 ? lastHour : UsageTally.Empty))];
	}

	internal static UsageSnapshot Snapshot()
	{
		var tmdb = Tally(answered: 90, after: 3, failed: 2);
		var plex = Tally(answered: 40, gaveUp: 1, cancelled: 4);
		var current = Period(
			[Line(RemoteService.Tmdb, tmdb), Line(RemoteService.Plex, plex)],
			[
				new UsageCauseLine(UsageCause.FeedRun, Tally(answered: 120, after: 3, gaveUp: 1, failed: 2)),
				new UsageCauseLine(UsageCause.Search, Tally(answered: 10, cancelled: 4)),
			]
		);
		return new UsageSnapshot(current, Period([Line(RemoteService.Tmdb, Tally(answered: 50))], []), []);
	}
}
