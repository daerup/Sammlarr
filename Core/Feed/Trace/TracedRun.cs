using System.Diagnostics;
using Domain.Feed.Run;
using Domain.Feed.Trace;
using Domain.Identity;
using Domain.Providers;

namespace Core.Feed.Trace;

internal readonly record struct TraceSummary(int Steps, int Peak, int Dropped);

internal sealed class TracedRun(Guid key, UserId owner, int cap)
{
	private static readonly TimeSpan Retention = TimeSpan.FromSeconds(30);

	private readonly Lock _gate = new();

	private readonly long _started = Stopwatch.GetTimestamp();

	private readonly List<TraceStep> _steps = [];

	private readonly Dictionary<int, Flight> _flights = [];

	private readonly Dictionary<HopPlace, NodeCount> _nodes = [];

	private readonly HashSet<(int Title, HopPlace Node)> _gaveUp = [];

	private readonly Fates _fates = new();

	private Optional<int> _requests = new None();

	private Optional<FeedRunEnd> _ended = new None();

	private Optional<SourcePage> _page = new None();

	private Optional<int> _reading = new None();

	private Optional<FeedRunCounts> _counts = new None();

	private long _endedAt;

	private int _titles;

	private int _dropped;

	private bool _running = true;

	internal Guid Key => key;

	internal UserId Owner => owner;

	internal bool Running
	{
		get
		{
			lock (_gate)
			{
				return _running;
			}
		}
	}

	internal bool Expired
	{
		get
		{
			lock (_gate)
			{
				return !_running && Stopwatch.GetElapsedTime(_endedAt) > Retention;
			}
		}
	}

	internal int StartTitle(string name, Optional<int> year)
	{
		lock (_gate)
		{
			var title = _titles++;
			Record(Some.Of(title), new TitleStarted(name, year));
			return title;
		}
	}

	internal void Add(Optional<int> title, TraceEvent traceEvent)
	{
		lock (_gate)
		{
			if (IsRepeat(title, traceEvent))
			{
				return;
			}

			Record(title, traceEvent);
		}
	}

	internal TraceRead Read(TraceCursor after)
	{
		lock (_gate)
		{
			return after.Run.When(run => run == key).Match<Guid, TraceRead>(_ => Batch(after.Seq), () => Snapshot());
		}
	}

	internal void Reading(Optional<int> page)
	{
		lock (_gate)
		{
			_reading = page.When(_ => _running);
		}
	}

	internal void Counted(FeedRunCounts counts)
	{
		lock (_gate)
		{
			_counts = Some.Of(counts);
		}
	}

	internal TraceSummary End(TraceEnding ending)
	{
		lock (_gate)
		{
			_ended = Some.Of(new FeedRunEnd(ending, Elapsed()) { Counts = _counts });
			_endedAt = Stopwatch.GetTimestamp();
			_requests = _requests.Or(Some.Of(_fates.Started));
			_flights.Clear();
			_reading = new None();
			foreach (var node in _nodes.Values)
			{
				node.Waiting = 0;
			}

			_running = false;
			var peak = _steps.CountBy(step => step.AtMs / 1000).Select(pair => pair.Value).DefaultIfEmpty().Max();
			return new TraceSummary(_steps.Count, peak, _dropped);
		}
	}

	private bool IsRepeat(Optional<int> title, TraceEvent traceEvent)
	{
		return traceEvent is GaveUp gaveUp && title.Match(index => !_gaveUp.Add((index, new HopPlace(gaveUp.Service, gaveUp.Stage))), () => false);
	}

	private void Record(Optional<int> title, TraceEvent traceEvent)
	{
		Apply(title, traceEvent);
		if (_steps.Count >= cap && traceEvent is PathStarted or HopAsked or HopAnswered or GaveUp)
		{
			_dropped++;
			return;
		}

		_steps.Add(new TraceStep(_steps.Count + 1, Elapsed(), title, traceEvent));
	}

	private void Apply(Optional<int> title, TraceEvent traceEvent)
	{
		var flight = title.Map(index => _flights.GetValueOrNone(index));
		switch (traceEvent)
		{
			case TitleStarted started:
				title.Apply(index => _flights[index] = new Flight(started.Name, started.Year));
				_fates.Started++;
				break;
			case SourceEnded ended:
				_requests = Some.Of(ended.Requests);
				break;
			case ListedWithoutIds:
				_fates.ListedWithoutIds++;
				break;
			case TitleKnown:
				_fates.Known++;
				break;
			case TitleNotFoundEarlier:
				_fates.Earlier++;
				break;
			case PathStarted:
				flight.Apply(found => found.At = new None());
				break;
			case HopAsked asked:
				Node(new HopPlace(asked.Service, asked.Stage)).Waiting++;
				flight.Apply(found => found.At = Some.Of(new HopPlace(asked.Service, asked.Stage)));
				break;
			case HopAnswered answered:
				Node(new HopPlace(answered.Service, answered.Stage)).Answer(answered.Outcome, answered.Ms);
				flight.Apply(found => found.Answered(answered));
				title.When(_ => answered.Outcome is HopOutcome.GaveUp).Apply(index => _gaveUp.Add((index, new HopPlace(answered.Service, answered.Stage))));
				break;
			case GaveUp gaveUp:
				Node(new HopPlace(gaveUp.Service, gaveUp.Stage)).GaveUp++;
				break;
			case SourcePage page:
				_page = Some.Of(page);
				break;
			case TitleSettled settled:
				title.Apply(index => _flights.Remove(index)).Apply(index => _gaveUp.RemoveWhere(entry => entry.Title == index));
				_fates.Settle(settled.Fate);
				break;
		}
	}

	private NodeCount Node(HopPlace place)
	{
		if (!_nodes.TryGetValue(place, out var node))
		{
			node = new NodeCount();
			_nodes[place] = node;
		}

		return node;
	}

	private int Elapsed() => _ended.Match(end => end.ElapsedMs, () => (int)Stopwatch.GetElapsedTime(_started).TotalMilliseconds);

	private TraceCursor Cursor() => new(Some.Of(key), _steps.Count);

	private RunProgress Progress()
	{
		var busiest = _nodes
			.GroupBy(pair => pair.Key.Service, pair => pair.Value.Waiting)
			.Select(group => new ServiceWait(group.Key, group.Sum()))
			.Where(wait => wait.Waiting > 0)
			.OrderByDescending(wait => wait.Waiting)
			.ThenBy(wait => wait.Service)
			.FirstOrNone();
		return new RunProgress(_page, _nodes.Values.Sum(node => node.Waiting)) { Busiest = busiest, Reading = _reading };
	}

	private TraceBatch Batch(long seq)
	{
		return new TraceBatch(Cursor(), Elapsed(), [.. _steps.Skip((int)seq)], _fates.Tally(), _requests, _ended) { Progress = Progress() };
	}

	private TraceSnapshot Snapshot()
	{
		return new TraceSnapshot(
			Cursor(),
			Elapsed(),
			[.. _flights.Select(pair => pair.Value.InFlight(pair.Key))],
			[.. _nodes.Select(pair => pair.Value.Tally(pair.Key))],
			_fates.Tally(),
			_requests,
			_ended
		)
		{
			Progress = Progress(),
		};
	}

	private sealed class Flight(string name, Optional<int> year)
	{
		private Optional<RemoteService> _lastIds = new None();

		private Optional<RemoteService> _lastDetails = new None();

		internal Optional<HopPlace> At { get; set; } = new None();

		internal void Answered(HopAnswered answered)
		{
			if (answered.Outcome is not (HopOutcome.Answered or HopOutcome.AfterRateLimit))
			{
				return;
			}

			switch (answered.Stage)
			{
				case TraceStage.Ids:
					_lastIds = Some.Of(answered.Service);
					break;
				case TraceStage.Details:
					_lastDetails = Some.Of(answered.Service);
					break;
			}
		}

		internal TitleInFlight InFlight(int title) => new(title, name, year, At, _lastIds, _lastDetails);
	}

	private sealed class NodeCount
	{
		private int _answered;

		private int _afterRateLimit;

		private int _failed;

		private int _answers;

		private long _totalMs;

		internal int Waiting { get; set; }

		internal int GaveUp { get; set; }

		internal void Answer(HopOutcome outcome, int ms)
		{
			Waiting--;
			_answers++;
			_totalMs += ms;
			switch (outcome)
			{
				case HopOutcome.Answered:
					_answered++;
					break;
				case HopOutcome.AfterRateLimit:
					_afterRateLimit++;
					break;
				case HopOutcome.GaveUp:
					GaveUp++;
					break;
				case HopOutcome.Failed:
					_failed++;
					break;
			}
		}

		internal NodeTally Tally(HopPlace node) => new(node, _answered, _afterRateLimit, GaveUp, _failed, Waiting, _answers == 0 ? 0 : (int)(_totalMs / _answers));
	}

	private sealed class Fates
	{
		private int _matched;

		private int _couldNotLookUp;

		private int _notFound;

		internal int Started { get; set; }

		internal int Known { get; set; }

		internal int Earlier { get; set; }

		internal int ListedWithoutIds { get; set; }

		internal void Settle(TitleFate fate)
		{
			switch (fate)
			{
				case TitleFate.Matched:
					_matched++;
					break;
				case TitleFate.CouldNotLookUp:
					_couldNotLookUp++;
					break;
				case TitleFate.NotFound:
					_notFound++;
					break;
			}
		}

		internal FateTally Tally() => new(Started, Known, Earlier, _matched, _couldNotLookUp, _notFound, ListedWithoutIds);
	}
}
