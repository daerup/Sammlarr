using System.Globalization;
using Contracts.Usage;
using Domain.Usage;

namespace UI.Features.Settings.Components.Usage;

internal enum RightNowTone
{
	Plain,
	Paused,
	Down,
}

internal sealed record RightNowText(string Text, Optional<string> Icon, RightNowTone Tone);

internal sealed record OutcomeLook(UsageOutcome Outcome, string Name, string Icon, string Color);

internal static class UsageView
{
	internal const string Accent = "var(--chart-1)";

	internal const string Grey = "var(--muted-foreground)";

	private const double Noticeable = 0.01;

	internal const string Tip = "border bg-popover px-2.5 py-2 text-xs text-popover-foreground shadow-lg";

	internal static readonly IReadOnlyList<OutcomeLook> Outcomes = [.. Enum.GetValues<UsageOutcome>().Select(Look)];

	internal static string Count(double count) => Math.Round(count).ToString("N0", CultureInfo.InvariantCulture);

	internal static string Number(double value) => FormattableString.Invariant($"{value:0.##}");

	internal static double StripAt(double milliseconds)
	{
		var buckets = DurationBuckets.UpToMs;
		var index = buckets.TakeWhile(upTo => milliseconds > upTo).Count();
		var lower = index == 0 ? 0 : buckets[index - 1];
		var upper = buckets[index] == DurationBuckets.Over ? lower * 1.5 : buckets[index];
		return (index + ((milliseconds - lower) / (upper - lower))) / buckets.Count;
	}

	internal static string Shade(long count, long busiest)
	{
		return count == 0
			? "color-mix(in oklab, var(--foreground) 4%, transparent)"
			: FormattableString.Invariant($"color-mix(in oklab, var(--usage-shade) {Math.Round(12 + (count * 88.0 / busiest))}%, var(--card))");
	}

	internal static string Calls(long count) => count == 1 ? "1 call" : $"{Count(count)} calls";

	internal static string Share(long part, long total)
	{
		return (total, part * 1000 < total && part > 0) switch
		{
			(0, _) => "0%",
			(_, true) => "<0.1%",
			_ => FormattableString.Invariant($"{part * 100.0 / total:0.0}%"),
		};
	}

	internal static string Tone(long part, long total, string trouble)
	{
		var share = total == 0 ? 0 : (double)part / total;
		var colour = (share > Noticeable, part) switch
		{
			(true, _) => trouble,
			(false, 0) => "text-muted-foreground",
			_ => string.Empty,
		};
		return $"text-right tabular-nums {colour}";
	}

	internal static string Duration(double milliseconds)
	{
		return milliseconds >= 1000 ? FormattableString.Invariant($"{milliseconds / 1000:0.0} s") : FormattableString.Invariant($"{Math.Round(milliseconds)} ms");
	}

	internal static Optional<double> Change(double now, double before) => Optional.When(before > 0, () => (now - before) / before);

	internal static string Bucket(int upToMs)
	{
		return upToMs == DurationBuckets.Over ? ">10 s" : $"≤{Bound(upToMs)}";
	}

	private static string Bound(int upToMs)
	{
		return upToMs >= 1000 ? FormattableString.Invariant($"{upToMs / 1000.0:0.#} s") : FormattableString.Invariant($"{upToMs} ms");
	}

	internal static string Slot(UsageRange range, DateTimeOffset at)
	{
		var local = at.ToLocalTime();
		return range switch
		{
			UsageRange.Day => local.ToString("HH:mm", CultureInfo.InvariantCulture),
			UsageRange.Week => local.ToString("ddd d", CultureInfo.InvariantCulture),
			UsageRange.Month => local.ToString("MMM d", CultureInfo.InvariantCulture),
		};
	}

	internal static string Window(UsageRange range)
	{
		return range switch
		{
			UsageRange.Day => "last 24 hours",
			UsageRange.Week => "last 7 days",
			UsageRange.Month => "last 30 days",
		};
	}

	internal static string Before(UsageRange range)
	{
		return range switch
		{
			UsageRange.Day => "vs the day before",
			UsageRange.Week => "vs the 7 days before",
			UsageRange.Month => "vs the 30 days before",
		};
	}

	internal static string Each(UsageRange range) => range is UsageRange.Day ? "hour" : "day";

	internal static OutcomeLook Look(UsageOutcome outcome)
	{
		return outcome switch
		{
			UsageOutcome.Answered => new(outcome, "Answered", "check", "var(--usage-answered)"),
			UsageOutcome.AnsweredAfter429 => new(outcome, "Rate-limited, then answered", "rotate-cw", "var(--usage-after-429)"),
			UsageOutcome.GaveUpOn429 => new(outcome, "Rate-limited, gave up", "octagon-x", "var(--usage-gave-up)"),
			UsageOutcome.Failed => new(outcome, "Failed", "circle-x", "var(--usage-failed)"),
			UsageOutcome.Cancelled => new(outcome, "Cancelled", "circle-slash", "var(--usage-cancelled)"),
		};
	}

	internal static (string Name, string Icon) Cause(UsageCause cause)
	{
		return cause switch
		{
			UsageCause.FeedRun => ("Feed runs", "rss"),
			UsageCause.WantlistSync => ("Wantlist sync", "bookmark"),
			UsageCause.Dataset => ("Datasets", "database"),
			UsageCause.Search => ("Search", "search"),
			UsageCause.Browse => ("Browse", "trending-up"),
			UsageCause.ShowingTitles => ("Showing titles", "image"),
			UsageCause.KeyTest => ("Connection tests", "plug-zap"),
			UsageCause.FeedEditor => ("Feed editor", "square-pen"),
			UsageCause.DetailsFill => ("Filling details", "list-plus"),
			UsageCause.Other => ("Untagged", "circle-dashed"),
		};
	}

	internal static Optional<string> CauseHint(UsageCause cause)
	{
		return cause switch
		{
			UsageCause.FeedRun or UsageCause.Dataset or UsageCause.Search or UsageCause.Browse or UsageCause.ShowingTitles => new None(),
			UsageCause.WantlistSync => Some.Of(
				"The sync with Radarr, Sonarr or Plex, and what reads them for it: the sync dialog, a title added from a page, a sink switch, removing a person."
			),
			UsageCause.FeedEditor => Some.Of("Previews in the feed editor and before Run now on the Feeds list, the link check, and saving a feed."),
			UsageCause.KeyTest => Some.Of(
				"Checks of an API key, a sign-in, or a Radarr or Sonarr connection: the Test buttons in Settings, and the check of a stored one when it is used again."
			),
			UsageCause.DetailsFill => Some.Of(
				"Fills missing details of wantlisted and blocklisted titles in the background, a few minutes after a run, a new key or a sign-in."
			),
			UsageCause.Other => Some.Of("Calls made outside every flow that marks its cause. Which page made them is not recorded."),
		};
	}

	internal static RightNowText RightNow(UsageRightNow rightNow, DateTimeOffset now)
	{
		return rightNow
			.PausedUntil.Map(until => new RightNowText($"Paused until {Clock(until, now)}", Some.Of("pause"), RightNowTone.Paused))
			.Or(() => rightNow.FailingSince.Map(since => new RightNowText($"Not answering since {Clock(since, now)}", Some.Of("circle-x"), RightNowTone.Down)))
			.Or(() => rightNow.RequestsLeft.Map(left => new RightNowText($"{Count(left)} requests left today", Some.Of("gauge"), RightNowTone.Plain)))
			.Or(() => rightNow.DownloadedAt.Map(at => new RightNowText($"Downloaded at {Clock(at, now)}", Some.Of("database"), RightNowTone.Plain)))
			.Or(() => rightNow.LastCall.Map(at => new RightNowText($"Last call {Clock(at, now)}", new None(), RightNowTone.Plain)))
			.GetValueOr(new RightNowText(string.Empty, new None(), RightNowTone.Plain));
	}

	internal static string Time(DateTimeOffset at) => at.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

	internal static string Clock(DateTimeOffset at, DateTimeOffset now)
	{
		var local = at.ToLocalTime();
		var format = local.Date == now.ToLocalTime().Date ? "HH:mm" : "MMM d, HH:mm";
		return local.ToString(format, CultureInfo.InvariantCulture);
	}
}
