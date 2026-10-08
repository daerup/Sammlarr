using Contracts;
using Contracts.Datasets;
using Contracts.Identity;
using Contracts.Providers;
using Contracts.Usage;
using Domain.Providers;
using Domain.Usage;

namespace Core.Usage;

internal sealed class UsageReport(
	IUsageStore store,
	IUsageLastCalls lastCalls,
	IRemoteCapacity capacity,
	IDatasetLoads loads,
	IMdblistRequestsLeft mdblist,
	IDateTimeProvider clock,
	ICurrentUser currentUser
) : IUsageReport
{
	private static readonly UsageRightNow Unknown = new(new None(), new None(), new None(), new None(), new None());

	public async Task<UsageSnapshot> ReadAsync(UsageRange range, CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		var now = clock.Now;
		var window = UsageWindow.Of(range, now.ToLocalTime());
		var before = window.Before();
		var rows = await store.ReadAsync(before.From, window.To, cancellationToken);
		var live = await LiveAsync(now, cancellationToken);
		return new UsageSnapshot(Period(window, rows, live), Period(before, rows, _ => Unknown));
	}

	private async Task<Func<RemoteService, UsageRightNow>> LiveAsync(DateTimeOffset now, CancellationToken cancellationToken)
	{
		var left = await mdblist.TodayAsync(cancellationToken);
		var downloads = new Dictionary<RemoteService, DateTimeOffset>();
		foreach (var (service, dataset) in DatasetNames.ByService)
		{
			(await loads.FindAsync(dataset, cancellationToken)).Apply(load => downloads[service] = load.LoadedAt);
		}

		return service => new UsageRightNow(
			lastCalls.Of(service),
			new None(),
			capacity.Of(service).BlockedUntil.When(until => until > now),
			left.When(_ => service == RemoteService.Mdblist),
			downloads.GetValueOrNone(service)
		);
	}

	private static UsagePeriod Period(UsageWindow window, UsageRows rows, Func<RemoteService, UsageRightNow> live)
	{
		var calls = rows.Calls.Where(row => window.Contains(row.Hour)).ToList();
		var attempts = rows.Attempts.Where(row => window.Contains(row.Hour)).ToList();
		var callsBySlot = calls.ToLookup(row => window.SlotOf(row.Hour));
		var attemptsBySlot = attempts.ToLookup(row => window.SlotOf(row.Hour));
		IReadOnlyList<UsageSlot> slots =
		[
			.. Enumerable.Range(0, window.Slots).Select(slot => new UsageSlot(window.SlotAt(slot), Tally(callsBySlot[slot], attemptsBySlot[slot]))),
		];
		var callsByService = calls.ToLookup(row => row.Service);
		var attemptsByService = attempts.ToLookup(row => row.Service);
		IReadOnlyList<UsageServiceLine> services =
		[
			.. calls
				.Select(row => row.Service)
				.Concat(attempts.Select(row => row.Service))
				.Distinct()
				.Select(service => Line(service, callsByService[service], attemptsByService[service], live(service)))
				.OrderByDescending(line => line.Tally.Total),
		];
		var attemptsByCause = attempts.ToLookup(row => row.Cause);
		IReadOnlyList<UsageCauseLine> causes =
		[
			.. calls
				.GroupBy(row => row.Cause)
				.Select(group => new UsageCauseLine(group.Key, Tally(group, attemptsByCause[group.Key])))
				.OrderByDescending(line => line.Tally.Total),
		];
		return new UsagePeriod(window, Tally(calls, attempts), slots, services, causes);
	}

	private static UsageServiceLine Line(RemoteService service, IEnumerable<UsageCalls> calls, IEnumerable<UsageAttempts> attempts, UsageRightNow live)
	{
		var hours = calls
			.GroupBy(row => row.Hour)
			.Select(hour => (Hour: hour.Key, Tally: Tally(hour, [])))
			.Where(hour => hour.Tally.Answered + hour.Tally.Errors > 0)
			.OrderByDescending(hour => hour.Hour)
			.ToList();
		var rightNow = live with
		{
			LastCall = live.LastCall.Or(() => hours.Select(hour => hour.Hour).FirstOrNone()),
			FailingSince = hours.TakeWhile(hour => hour.Tally.Answered == 0).Select(hour => hour.Hour).Order().FirstOrNone(),
		};
		return new UsageServiceLine(service, Tally(calls, attempts), rightNow);
	}

	private static UsageTally Tally(IEnumerable<UsageCalls> calls, IEnumerable<UsageAttempts> attempts)
	{
		return new UsageTally(
			calls.GroupBy(row => row.Outcome).ToDictionary(group => group.Key, group => group.Sum(row => row.Calls)),
			attempts.SelectMany(row => row.Buckets).GroupBy(pair => pair.Key).ToDictionary(group => group.Key, group => group.Sum(pair => pair.Value))
		);
	}
}
