using System.Globalization;
using Contracts.Datasets;
using Contracts.Housekeeping;
using Domain.Providers;
using Shared.Options;
using UI.Features.Settings.Components.Usage;

namespace UI.Features.Settings.Components.Housekeeping;

public enum TaskTone
{
	Quiet,
	Ok,
	Bad,
	Running,
}

public sealed record TaskLine(TaskTone Tone, string When, Optional<string> Count, string Did, Optional<string> Error);

internal sealed record DatasetLook(string Name, string What, string Noun, string Href);

internal static class HousekeepingView
{
	internal const string Nothing = "—";

	private const string Waiting = "waiting for its turn";

	private const long Megabyte = 1024 * 1024;

	private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

	private static readonly int StepTotal = Enum.GetValues<HousekeepingStep>().Length;

	private static readonly TimeSpan StoredPrecision = TimeSpan.FromTicks(1000);

	internal static readonly IReadOnlyDictionary<RemoteService, DatasetLook> Datasets = new Dictionary<RemoteService, DatasetLook>
	{
		[RemoteService.ImdbDatasets] = new("IMDb ratings", "rates titles by IMDb id", "ratings", "https://data.imdb.com/non-commercial-datasets/"),
		[RemoteService.AnimeLists] = new("Anime ids", "maps AniList and MyAnimeList ids", "mappings", "https://github.com/Fribb/anime-lists"),
	};

	internal static TaskLine Pass(PassTask pass, DateTimeOffset now)
	{
		return pass.Running.Match(run => Running(run, Progress(pass.Last, run)), () => pass.Last.Map(report => Finished(report, now)).GetValueOr(Never));
	}

	internal static TaskLine Dataset(DatasetTask dataset, DateTimeOffset now)
	{
		var noun = Datasets[dataset.Service].Noun;
		return dataset.Running.Match(
			run => Running(run, run.Started ? "checking for a new file" : Waiting),
			() =>
				dataset
					.Last.Map(check => Checked(check, dataset.Loaded, noun, now))
					.Or(() => dataset.Loaded.Map(load => Loaded(Day(load.LoadedAt, now), Some.Of(Number(load.Rows)), noun)))
					.GetValueOr(Never)
		);
	}

	internal static string Runs(Optional<DateTimeOffset> next) => next.Map(at => $"daily {UsageView.Time(at)}").GetValueOr("by hand");

	internal static string Next(Optional<DateTimeOffset> next, DateTimeOffset now) => next.Map(at => Day(at, now)).GetValueOr(Nothing);

	internal static string Size(long bytes)
	{
		return bytes >= 100 * Megabyte ? string.Create(Invariant, $"{bytes / Megabyte} MB") : string.Create(Invariant, $"{(double)bytes / Megabyte:0.0} MB");
	}

	internal static bool Bloated(DatabaseSize size) => size.OnDisk >= size.Data * 3 / 2;

	internal static Optional<DateTimeOffset> CompactedAt(HousekeepingOverview overview)
	{
		Optional<DateTimeOffset>[] compactions =
		[
			overview.Pass.Last.Map(report => report.File.Map(_ => report.FinishedAt)),
			.. overview.Datasets.Select(dataset => dataset.Loaded.Map(load => load.LoadedAt)),
		];
		return compactions.WhereSome().OrderDescending().FirstOrNone();
	}

	internal static string Removed(HousekeepingReport report, DateTimeOffset now)
	{
		var days = DaysFrom(report.StartedAt, now);
		var time = UsageView.Time(report.StartedAt);
		return days switch
		{
			0 => $"Removed by today's pass, {time}",
			-1 => $"Removed by yesterday's pass, {time}",
			_ => $"Removed by the pass of {report.StartedAt.ToLocalTime().ToString("MMM d", Invariant)}, {time}",
		};
	}

	internal static IReadOnlyList<(string Label, int Removed)> Breakdown(HousekeepingReport report, HousekeepingOptions options)
	{
		return
		[
			.. report
				.Steps.OrderBy(step => step.Step is HousekeepingStep.UsageStats)
				.GroupBy(step => Label(step.Step, options), StringComparer.Ordinal)
				.Select(steps => (steps.Key, steps.Sum(step => step.Removed))),
		];
	}

	internal static string Day(DateTimeOffset at, DateTimeOffset now)
	{
		var day = DaysFrom(at, now) switch
		{
			-1 => "yesterday",
			0 => "today",
			1 => "tomorrow",
			_ => at.ToLocalTime().ToString("MMM d", Invariant),
		};
		return $"{day} {UsageView.Time(at)}";
	}

	internal static string Days(int days) => days == 365 ? "a year" : FormattableString.CurrentCulture($"{days} days");

	internal static string Titles(int count) => count == 1 ? "1 title" : $"{Number(count)} titles";

	internal static string Number(int count) => count.ToString("N0", Invariant);

	private static TaskLine Never => new(TaskTone.Quiet, "never run", new None(), Nothing, new None());

	private static int DaysFrom(DateTimeOffset at, DateTimeOffset now)
	{
		return DateOnly.FromDateTime(at.ToLocalTime().DateTime).DayNumber - DateOnly.FromDateTime(now.ToLocalTime().DateTime).DayNumber;
	}

	private static TaskLine Running(TaskRun run, string did)
	{
		var when = run.Started ? $"running since {UsageView.Time(run.Since)}" : $"queued at {UsageView.Time(run.Since)}";
		return new TaskLine(TaskTone.Running, when, new None(), did, new None());
	}

	private static string Progress(Optional<HousekeepingReport> last, TaskRun run)
	{
		return last.When(report => run.Started && report.FinishedAt.IsNone() && report.StartedAt + StoredPrecision > run.Since)
			.Map(report => FormattableString.CurrentCulture($"{report.Steps.Count} of {StepTotal} steps done"))
			.GetValueOr(Waiting);
	}

	private static TaskLine Finished(HousekeepingReport report, DateTimeOffset now)
	{
		var removed = report.Steps.Sum(step => step.Removed);
		var when = Day(report.StartedAt, now);
		return report.FinishedAt.Match(
			_ =>
				report.Failure.Match(failure => Failed($"{when} · failed", removed, $"Stopped at {Name(failure.Step)}: {failure.Reason}"), () => Done(when, removed)),
			() => Failed($"{when} · stopped", removed, "Sammlarr stopped before the pass finished.")
		);
	}

	private static TaskLine Done(string when, int removed)
	{
		return removed == 0
			? new TaskLine(TaskTone.Quiet, $"{when} · ok", new None(), "nothing to remove", new None())
			: new TaskLine(TaskTone.Ok, $"{when} · ok", Some.Of(Number(removed)), Rows(removed), new None());
	}

	private static TaskLine Failed(string when, int removed, string error)
	{
		return new TaskLine(TaskTone.Bad, when, Optional.When(removed > 0, () => Number(removed)), removed > 0 ? Rows(removed) : "nothing removed", Some.Of(error));
	}

	private static TaskLine Checked(DatasetCheck check, Optional<DatasetLoad> loaded, string noun, DateTimeOffset now)
	{
		var when = Day(check.StartedAt, now);
		var rows = loaded.Map(load => Number(load.Rows));
		return check.Outcome switch
		{
			DatasetOutcome.Loaded => Loaded(when, rows, noun),
			DatasetOutcome.Unchanged => new TaskLine(TaskTone.Quiet, $"{when} · unchanged", rows, $"{noun} · unchanged", new None()),
			DatasetOutcome.Failed => new TaskLine(TaskTone.Bad, $"{when} · failed", new None(), Kept(loaded, noun), check.Failure),
		};
	}

	private static TaskLine Loaded(string when, Optional<string> rows, string noun) => new(TaskTone.Ok, $"{when} · loaded", rows, noun, new None());

	private static string Kept(Optional<DatasetLoad> loaded, string noun)
	{
		return loaded.Map(load => $"kept the {noun} of {load.LoadedAt.ToLocalTime().ToString("MMM d", Invariant)}").GetValueOr($"no {noun} yet");
	}

	private static string Label(HousekeepingStep step, HousekeepingOptions options)
	{
		return step switch
		{
			HousekeepingStep.RunHistory => $"Feed history: titles and calls of runs older than {Days(options.RunHistoryDays)}",
			HousekeepingStep.UsageStats => $"Usage stats older than {Days(options.UsageDays)}",
			HousekeepingStep.CachedDetails => $"Cached details nobody opened for {Days(options.DetailsUnusedDays)}",
			HousekeepingStep.TitleIds => $"Title ids nobody used for {Days(options.IdsUnusedDays)}",
			HousekeepingStep.DuplicateTitles => "Titles stored twice, merged once a second source confirms them",
			HousekeepingStep.SettledJobs or HousekeepingStep.ScheduleEntries => "Finished jobs and schedule entries, 30 days",
		};
	}

	private static string Name(Optional<HousekeepingStep> step)
	{
		return step.Map(at =>
				at switch
				{
					HousekeepingStep.RunHistory => "feed history",
					HousekeepingStep.UsageStats => "usage stats",
					HousekeepingStep.CachedDetails => "cached details",
					HousekeepingStep.TitleIds => "title ids",
					HousekeepingStep.DuplicateTitles => "titles stored twice",
					HousekeepingStep.SettledJobs => "finished jobs",
					HousekeepingStep.ScheduleEntries => "schedule entries",
				}
			)
			.GetValueOr("the compaction");
	}

	private static string Rows(int removed) => removed == 1 ? "row removed" : "rows removed";
}
