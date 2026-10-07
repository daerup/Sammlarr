using Contracts;
using Contracts.Identity;
using Contracts.Usage;
using Domain.Providers;
using Domain.Usage;

namespace Core.Usage;

internal sealed class UsageReport(IUsageStore store, IUsageLastCalls lastCalls, IDateTimeProvider clock, ICurrentUser currentUser) : IUsageReport
{
	public async Task<UsageSnapshot> ReadAsync(UsageRange range, CancellationToken cancellationToken = default)
	{
		currentUser.EnsureAdmin();
		var window = UsageWindow.Of(range, clock.Now.ToLocalTime());
		var before = window.Before();
		var rows = await store.ReadAsync(before.From, window.To, cancellationToken);
		return new UsageSnapshot(Period(window, rows, service => lastCalls.Of(service)), Period(before, rows, _ => new None()));
	}

	private static UsagePeriod Period(UsageWindow window, UsageRows rows, Func<RemoteService, Optional<DateTimeOffset>> remembered)
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
				.Select(service => Line(service, callsByService[service], attemptsByService[service], remembered))
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

	private static UsageServiceLine Line(
		RemoteService service,
		IEnumerable<UsageCalls> calls,
		IEnumerable<UsageAttempts> attempts,
		Func<RemoteService, Optional<DateTimeOffset>> remembered
	)
	{
		var hours = calls
			.GroupBy(row => row.Hour)
			.Select(hour => (Hour: hour.Key, Tally: Tally(hour, [])))
			.Where(hour => hour.Tally.Answered + hour.Tally.Errors > 0)
			.OrderByDescending(hour => hour.Hour)
			.ToList();
		var failingSince = hours.TakeWhile(hour => hour.Tally.Answered == 0).Select(hour => hour.Hour).Order().FirstOrNone();
		return new UsageServiceLine(service, Tally(calls, attempts), remembered(service).Or(() => hours.Select(hour => hour.Hour).FirstOrNone()), failingSince);
	}

	private static UsageTally Tally(IEnumerable<UsageCalls> calls, IEnumerable<UsageAttempts> attempts)
	{
		return new UsageTally(
			calls.GroupBy(row => row.Outcome).ToDictionary(group => group.Key, group => group.Sum(row => row.Calls)),
			attempts.SelectMany(row => row.Buckets).GroupBy(pair => pair.Key).ToDictionary(group => group.Key, group => group.Sum(pair => pair.Value))
		);
	}
}
