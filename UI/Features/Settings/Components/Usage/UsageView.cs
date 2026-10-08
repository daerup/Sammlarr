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
	internal const string Accent = "var(--usage-accent)";

	internal const string Grey = "var(--muted-foreground)";

	internal static readonly IReadOnlyList<OutcomeLook> Outcomes = [.. Enum.GetValues<UsageOutcome>().Select(Look)];

	internal static string Count(double count) => Math.Round(count).ToString("N0", CultureInfo.InvariantCulture);

	internal static string Share(long part, long total)
	{
		return total == 0 ? "0%" : FormattableString.Invariant($"{part * 100.0 / total:0.0}%");
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
			UsageCause.KeyTest => ("Key and sign-in tests", "key-round"),
			UsageCause.Other => ("Other", "circle-help"),
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

	internal static string Clock(DateTimeOffset at, DateTimeOffset now)
	{
		var local = at.ToLocalTime();
		var format = local.Date == now.ToLocalTime().Date ? "HH:mm" : "MMM d, HH:mm";
		return local.ToString(format, CultureInfo.InvariantCulture);
	}
}
