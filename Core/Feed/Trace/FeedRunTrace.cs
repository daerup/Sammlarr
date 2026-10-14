using System.Collections.Concurrent;
using System.Diagnostics;
using Contracts.Feed;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Feed.Trace;
using Domain.Identity;
using Domain.Providers;
using Microsoft.Extensions.Logging;
using Observability.Feed;
using Observability.Usage;

namespace Core.Feed.Trace;

internal sealed class FeedRunTrace : IFeedRunTrace, IDisposable
{
	private const int DefaultCap = 50_000;

	private const string HttpSource = "System.Net.Http";

	private const string StatusTag = "http.response.status_code";

	private readonly ConcurrentDictionary<FeedId, TracedRun> _runs = new();

	private readonly ConcurrentDictionary<ActivitySpanId, TracedRun> _runSpans = new();

	private readonly ConcurrentDictionary<ActivitySpanId, TracedTitle> _titles = new();

	private readonly ConcurrentDictionary<ActivitySpanId, TracedHop> _hops = new();

	private readonly ActivityListener _listener;

	private readonly ILogger<FeedRunTrace> _logger;

	private readonly int _cap;

	public FeedRunTrace(ILogger<FeedRunTrace> logger)
		: this(logger, DefaultCap) { }

	internal FeedRunTrace(ILogger<FeedRunTrace> logger, int cap)
	{
		_logger = logger;
		_cap = cap;
		_listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name is FeedRunSpans.SourceName or HttpSource,
			Sample = Sample,
			ActivityStarted = Started,
			ActivityStopped = Stopped,
		};
		ActivitySource.AddActivityListener(_listener);
	}

	public IDisposable Start(FeedId feed, UserId owner)
	{
		Sweep();
		var run = new TracedRun(Guid.NewGuid(), owner, _cap);
		_runs.GetValueOrNone(feed).Apply(Forget);
		_runs[feed] = run;
		return new RunScope(_runSpans, run, FeedRunSpans.Run(feed, run.Key));
	}

	public void Counted(FeedId feed, FeedRunCounts counts)
	{
		_runs.GetValueOrNone(feed).When(run => run.Running).Apply(run => run.Counted(counts));
	}

	public void End(FeedId feed, FeedRunResult result)
	{
		_runs.GetValueOrNone(feed).When(run => run.Running).Apply(run => Finish(feed, run, Ending(result)));
	}

	internal TraceRead Read(FeedId feed, UserId viewer, TraceCursor after)
	{
		Sweep();
		return _runs.GetValueOrNone(feed).When(run => run.Owner == viewer).Match(run => run.Read(after), () => new NoTrace());
	}

	public void Dispose() => _listener.Dispose();

	private void Sweep()
	{
		foreach (var (feed, run) in _runs.Where(pair => pair.Value.Expired))
		{
			_runs.TryRemove(KeyValuePair.Create(feed, run));
		}
	}

	private ActivitySamplingResult Sample(ref ActivityCreationOptions<ActivityContext> options)
	{
		if (options.Source.Name != HttpSource)
		{
			return ActivitySamplingResult.AllData;
		}

		return HopAbove(Activity.Current).Match(_ => ActivitySamplingResult.AllData, () => ActivitySamplingResult.None);
	}

	private void Started(Activity span)
	{
		switch (span.Source.Name, span.OperationName)
		{
			case (FeedRunSpans.SourceName, FeedRunNames.Title):
				RunAbove(span.Parent).Apply(run => StartTitle(span, run));
				break;
			case (FeedRunSpans.SourceName, FeedRunNames.Path):
				TitleAbove(span.Parent).Apply(title => title.Add(new PathStarted()));
				break;
			case (FeedRunSpans.SourceName, FeedRunNames.Hop):
				TitleAbove(span.Parent).Apply(title => PlaceOf(span).Apply(place => StartHop(span, title, place)));
				break;
			case (FeedRunSpans.SourceName, FeedRunNames.Reading):
				RunAbove(span.Parent).Apply(run => run.Reading(Tag<int>(span, FeedRunTags.Page)));
				break;
		}
	}

	private void Stopped(Activity span)
	{
		switch (span.Source.Name, span.OperationName)
		{
			case (HttpSource, _):
				HopAbove(span.Parent).Apply(hop => hop.Called(Tag<int>(span, StatusTag)));
				break;
			case (FeedRunSpans.SourceName, FeedRunNames.Title):
				SettleTitle(span);
				break;
			case (FeedRunSpans.SourceName, FeedRunNames.Hop):
				AnswerHop(span);
				break;
			case (FeedRunSpans.SourceName, FeedRunNames.Wait):
				TitleAbove(span.Parent).Apply(title => PlaceOf(span).Apply(place => title.Add(new GaveUp(place.Service, place.Stage))));
				break;
			case (FeedRunSpans.SourceName, FeedRunNames.Source):
				RunAbove(span.Parent).Apply(run => Tag<int>(span, FeedRunTags.Requests).Apply(requests => run.Add(new None(), new SourceEnded(requests))));
				break;
			case (FeedRunSpans.SourceName, FeedRunNames.Listed):
				RunAbove(span.Parent).Apply(run => run.Add(new None(), new ListedWithoutIds()));
				break;
			case (FeedRunSpans.SourceName, FeedRunNames.Reading):
				RunAbove(span.Parent).Apply(run => run.Reading(new None()));
				break;
			case (FeedRunSpans.SourceName, FeedRunNames.Page):
				RunAbove(span.Parent)
					.Apply(run => Tag<int>(span, FeedRunTags.Page).Apply(page => run.Add(new None(), new SourcePage(page, Tag<int>(span, FeedRunTags.Pages)))));
				break;
		}
	}

	private void StartTitle(Activity span, TracedRun run)
	{
		Tag<string>(span, FeedRunTags.Name).Apply(name => _titles[span.SpanId] = new TracedTitle(run, run.StartTitle(name, Tag<int>(span, FeedRunTags.Year))));
	}

	private void StartHop(Activity span, TracedTitle title, HopPlace place)
	{
		var hop = new TracedHop(title, place.Service, place.Stage);
		_hops[span.SpanId] = hop;
		title.Add(new HopAsked(hop.Service, hop.Stage));
	}

	private void SettleTitle(Activity span)
	{
		if (!_titles.TryRemove(span.SpanId, out var title))
		{
			return;
		}

		Tag<bool>(span, FeedRunTags.Known).When(known => known).Apply(_ => title.Add(new TitleKnown()));
		Tag<bool>(span, FeedRunTags.Earlier).When(earlier => earlier).Apply(_ => title.Add(new TitleNotFoundEarlier()));
		title.Add(new TitleSettled(Tag<TitleFate>(span, FeedRunTags.Fate).GetValueOr(TitleFate.CouldNotLookUp)));
	}

	private void AnswerHop(Activity span)
	{
		if (!_hops.TryRemove(span.SpanId, out var hop))
		{
			return;
		}

		var answer = Tag<HopOutcome>(span, FeedRunTags.Answer).GetValueOr(HopOutcome.Failed);
		hop.Title.Add(new HopAnswered(hop.Service, hop.Stage, hop.Outcome(answer), (int)span.Duration.TotalMilliseconds));
	}

	private void Finish(FeedId feed, TracedRun run, TraceEnding ending)
	{
		var summary = run.End(ending);
		Forget(run);
		_logger.LogInformation(
			"Feed run trace {FeedId}: {Steps} steps, {Peak} in the busiest second, {Dropped} hops past the cap",
			feed,
			summary.Steps,
			summary.Peak,
			summary.Dropped
		);
	}

	private void Forget(TracedRun run)
	{
		foreach (var (id, _) in _titles.Where(pair => pair.Value.Run == run))
		{
			_titles.TryRemove(id, out _);
		}

		foreach (var (id, _) in _hops.Where(pair => pair.Value.Title.Run == run))
		{
			_hops.TryRemove(id, out _);
		}
	}

	private Optional<TracedRun> RunAbove(Activity? span) => Lineage(span).Select(each => _runSpans.GetValueOrNone(each.SpanId)).WhereSome().FirstOrNone();

	private Optional<TracedTitle> TitleAbove(Activity? span) => Lineage(span).Select(each => _titles.GetValueOrNone(each.SpanId)).WhereSome().FirstOrNone();

	private Optional<TracedHop> HopAbove(Activity? span) => Lineage(span).Select(each => _hops.GetValueOrNone(each.SpanId)).WhereSome().FirstOrNone();

	private static IEnumerable<Activity> Lineage(Activity? span)
	{
		for (var each = span; each is not null; each = each.Parent)
		{
			yield return each;
		}
	}

	private static TraceEnding Ending(FeedRunResult result)
	{
		return result switch
		{
			FeedRunCompleted => TraceEnding.Completed,
			FeedRunFailed => TraceEnding.Failed,
			FeedRunAlreadyRunning => TraceEnding.Failed,
		};
	}

	private static Optional<HopPlace> PlaceOf(Activity span)
	{
		return Tag<RemoteService>(span, UsageTags.Service).Map(service => Tag<TraceStage>(span, FeedRunTags.Stage).Map(stage => new HopPlace(service, stage)));
	}

	private static Optional<T> Tag<T>(Activity span, string name)
		where T : notnull
	{
		return span.GetTagItem(name) is T value ? Some.Of(value) : new None();
	}

	private sealed class RunScope : IDisposable
	{
		private readonly ConcurrentDictionary<ActivitySpanId, TracedRun> _runSpans;

		private readonly Optional<Activity> _span;

		internal RunScope(ConcurrentDictionary<ActivitySpanId, TracedRun> runSpans, TracedRun run, Optional<Activity> span)
		{
			_runSpans = runSpans;
			_span = span;
			span.Apply(found => runSpans[found.SpanId] = run);
		}

		public void Dispose()
		{
			_span.Apply(found => _runSpans.TryRemove(found.SpanId, out _)).Apply(found => found.Stop());
		}
	}

	private sealed record TracedTitle(TracedRun Run, int Index)
	{
		internal void Add(TraceEvent traceEvent) => Run.Add(Some.Of(Index), traceEvent);
	}

	private sealed class TracedHop(TracedTitle title, RemoteService service, TraceStage stage)
	{
		private const int RateLimited = 429;

		private readonly Lock _gate = new();

		private bool _rateLimited;

		private bool _gaveUp;

		internal TracedTitle Title => title;

		internal RemoteService Service => service;

		internal TraceStage Stage => stage;

		internal void Called(Optional<int> status)
		{
			var rateLimited = status.Match(code => code == RateLimited, () => false);
			var unanswered = status.Match(_ => false, () => true);
			lock (_gate)
			{
				_gaveUp = rateLimited || (_rateLimited && unanswered);
				_rateLimited |= rateLimited;
			}
		}

		internal HopOutcome Outcome(HopOutcome answer)
		{
			lock (_gate)
			{
				return (_gaveUp, answer, _rateLimited) switch
				{
					(true, _, _) => HopOutcome.GaveUp,
					(false, HopOutcome.Answered, true) => HopOutcome.AfterRateLimit,
					_ => answer,
				};
			}
		}
	}
}
