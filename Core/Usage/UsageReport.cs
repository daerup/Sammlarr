using Contracts;
using Contracts.Datasets;
using Contracts.Identity;
using Contracts.Providers;
using Contracts.Usage;
using Domain.Feed;
using Domain.Providers;
using Domain.Usage;

namespace Core.Usage;

internal sealed class UsageReport(
	IUsageStore store,
	IUsageLastCalls lastCalls,
	IRemoteCapacity capacity,
	IDatasetLoads loads,
	IMdblistRequestsLeft mdblist,
	UsageFeedLines feedLines,
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
		var today = await mdblist.TodayAsync(cancellationToken);
		var live = await LiveAsync(now, today, cancellationToken);
		var feeds = await feedLines.ReadAsync(window, rows, cancellationToken);
		IReadOnlyList<UsageQuota> quotas = [.. new[] { today }.WhereSome().Select(found => Quota(found, rows, window, now))];
		return new UsageSnapshot(Period(window, rows, live, feeds.Names), Period(before, rows, _ => Unknown, feeds.Names), feeds.Lines, quotas);
	}

	private static UsageQuota Quota(QuotaToday today, UsageRows rows, UsageWindow window, DateTimeOffset now)
	{
		var spent = rows.Attempts.Where(row => row.Service == RemoteService.Mdblist).Select(row => (row.Hour, row.Cause, Spent: row.Attempts));
		return new UsageQuota(
			RemoteService.Mdblist,
			today,
			today.Answered.Map(allowance => QuotaDay.Of(Spent(rows), allowance, now)),
			QuotaDays.Of(spent, today.NextReset, (int)Math.Round((window.To - window.From) / TimeSpan.FromDays(1)))
		);
	}

	private static IReadOnlyList<(DateTimeOffset Hour, long Spent)> Spent(UsageRows rows)
	{
		return
		[
			.. rows.Attempts.Where(row => row.Service == RemoteService.Mdblist).GroupBy(row => row.Hour).Select(hour => (hour.Key, hour.Sum(row => row.Attempts))),
		];
	}

	private async Task<Func<RemoteService, UsageRightNow>> LiveAsync(DateTimeOffset now, Optional<QuotaToday> today, CancellationToken cancellationToken)
	{
		var left = today.Map(found => found.Answered.Map(allowance => allowance.Remaining));
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

	private static UsagePeriod Period(
		UsageWindow window,
		UsageRows rows,
		Func<RemoteService, UsageRightNow> live,
		IReadOnlyDictionary<FeedId, UsageFeedName> names
	)
	{
		var calls = rows.Calls.Where(row => window.Contains(row.Hour)).ToList();
		var attempts = rows.Attempts.Where(row => window.Contains(row.Hour)).ToList();
		var callsByService = calls.ToLookup(row => row.Service);
		var attemptsByService = attempts.ToLookup(row => row.Service);
		IReadOnlyList<UsageServiceLine> services =
		[
			.. calls
				.Select(row => row.Service)
				.Concat(attempts.Select(row => row.Service))
				.Distinct()
				.Select(service => Line(window, service, [.. callsByService[service]], [.. attemptsByService[service]], live(service), names))
				.OrderByDescending(line => line.Tally.Total),
		];
		return new UsagePeriod(window, Tally(calls, attempts), Slots(window, calls, attempts), services, Causes(calls, attempts, names));
	}

	private static IReadOnlyList<UsageCauseLine> Causes(
		IReadOnlyList<UsageCalls> calls,
		IReadOnlyList<UsageAttempts> attempts,
		IReadOnlyDictionary<FeedId, UsageFeedName> names
	)
	{
		var attemptsByCause = attempts.ToLookup(row => row.Cause);
		return
		[
			.. calls
				.GroupBy(row => row.Cause)
				.Select(group => new UsageCauseLine(group.Key, Tally(group, attemptsByCause[group.Key]), group.Key is UsageCause.FeedEditor ? Feeds(group, names) : []))
				.OrderByDescending(line => line.Tally.Total),
		];
	}

	private static IReadOnlyList<UsageCauseFeed> Feeds(IEnumerable<UsageCalls> calls, IReadOnlyDictionary<FeedId, UsageFeedName> names)
	{
		var byFeed = calls.ToLookup(row => row.Feed.When(names.ContainsKey).ToNullable());
		var unknown = Optional.When(byFeed.Contains(null), () => new UsageCauseFeed(new None(), byFeed[null].Sum(row => row.Calls)));
		return
		[
			.. byFeed
				.Where(feed => feed.Key is not null)
				.Select(feed => new UsageCauseFeed(Some.Of(names[feed.Key!.Value]), feed.Sum(row => row.Calls)))
				.OrderByDescending(feed => feed.Calls),
			.. new[] { unknown }.WhereSome(),
		];
	}

	private static IReadOnlyList<UsageSlot> Slots(UsageWindow window, IReadOnlyList<UsageCalls> calls, IReadOnlyList<UsageAttempts> attempts)
	{
		var callsBySlot = calls.ToLookup(row => window.SlotOf(row.Hour));
		var attemptsBySlot = attempts.ToLookup(row => window.SlotOf(row.Hour));
		return
		[
			.. Enumerable
				.Range(0, window.Slots)
				.Select(slot => new UsageSlot(window.SlotAt(slot), Tally(callsBySlot[slot], attemptsBySlot[slot]), CallsByCause(callsBySlot[slot]))),
		];
	}

	private static IReadOnlyDictionary<UsageCause, long> CallsByCause(IEnumerable<UsageCalls> calls)
	{
		return calls.GroupBy(row => row.Cause).ToDictionary(group => group.Key, group => group.Sum(row => row.Calls));
	}

	private static UsageServiceLine Line(
		UsageWindow window,
		RemoteService service,
		IReadOnlyList<UsageCalls> calls,
		IReadOnlyList<UsageAttempts> attempts,
		UsageRightNow live,
		IReadOnlyDictionary<FeedId, UsageFeedName> names
	)
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
		return new UsageServiceLine(service, Tally(calls, attempts), rightNow, Slots(window, calls, attempts), Causes(calls, attempts, names));
	}

	private static UsageTally Tally(IEnumerable<UsageCalls> calls, IEnumerable<UsageAttempts> attempts)
	{
		return new UsageTally(
			calls.GroupBy(row => row.Outcome).ToDictionary(group => group.Key, group => group.Sum(row => row.Calls)),
			attempts.SelectMany(row => row.Buckets).GroupBy(pair => pair.Key).ToDictionary(group => group.Key, group => group.Sum(pair => pair.Value))
		);
	}
}
