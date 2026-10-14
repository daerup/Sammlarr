using System.Globalization;
using Domain.Feed.Run;
using Domain.Providers;
using Domain.Usage;
using UI.Features.Settings.Components.Usage;

namespace UI.Features.Feeds.Components;

public sealed record RunTypical(double Calls, int Runs);

internal static class RunCallsView
{
	private static long Total(FeedRun run) => run.Calls.Sum(call => call.Calls);

	internal static IReadOnlyList<(RemoteService Service, UsageTally Tally)> Services(FeedRun run)
	{
		var calls = run.Calls.ToLookup(call => call.Service);
		var times = run.Times.ToLookup(time => time.Service);
		return
		[
			.. calls
				.Select(group => group.Key)
				.Union(times.Select(group => group.Key))
				.Select(service =>
					(
						Service: service,
						Tally: new UsageTally(
							calls[service].ToDictionary(call => call.Outcome, call => call.Calls),
							times[service].ToDictionary(time => time.UpToMs, time => time.Attempts)
						)
					)
				)
				.OrderByDescending(line => line.Tally.Total),
		];
	}

	internal static Optional<RunTypical> Typical(IEnumerable<FeedRun> runs)
	{
		var calls = runs.Select(Total).Where(total => total > 0).Order().ToList();
		return Optional.When(calls.Count > 0, () => new RunTypical(Median(calls), calls.Count));
	}

	internal static (string Text, string Icon) Versus(long calls, double typical)
	{
		var ratio = calls / typical;
		return (Math.Abs(ratio - 1) < 0.15, ratio > 1) switch
		{
			(true, _) => ("About the same as this feed's typical run", "minus"),
			(false, true) => (FormattableString.Invariant($"{ratio:0.0}× this feed's typical run"), "trending-up"),
			(false, false) => ($"{Math.Round((1 - ratio) * 100).ToString(CultureInfo.InvariantCulture)}% under this feed's typical run", "trending-down"),
		};
	}

	internal static string Basis(RunTypical typical)
	{
		var runs = typical.Runs == 1 ? "its 1 run" : FormattableString.CurrentCulture($"its {typical.Runs} runs");
		return $"({UsageView.Calls((long)Math.Round(typical.Calls))}, the median of {runs} in the last 30 days)";
	}

	private static double Median(IReadOnlyList<long> sorted)
	{
		var middle = sorted.Count / 2;
		return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2.0;
	}
}
