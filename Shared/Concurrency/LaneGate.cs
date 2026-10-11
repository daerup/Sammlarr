namespace Shared.Concurrency;

public sealed class LaneGate : IDisposable
{
	private static readonly TimeSpan LongestWake = TimeSpan.FromDays(1);

	private readonly Lock _gate = new();
	private readonly PriorityQueue<Ticket, (int Class, long Order, long Seq)> _waiting = new();
	private readonly LanePolicy _policy;
	private readonly TimeProvider _time;
	private readonly IReadOnlyList<WindowLog> _windows;
	private readonly ITimer _wake;
	private readonly Optional<ILaneQuota> _quota;
	private readonly string _owner;
	private long _seq;
	private int _inFlight;
	private int _failures;
	private double _scale = 1;
	private DateTimeOffset _halved = DateTimeOffset.MinValue;
	private DateTimeOffset _raised = DateTimeOffset.MinValue;
	private DateTimeOffset _throttled = DateTimeOffset.MinValue;
	private Optional<DateTimeOffset> _blockedUntil = new None();
	private Optional<DateTimeOffset> _downUntil = new None();
	private bool _probing;

	public LaneGate(LanePolicy policy, TimeProvider time, Optional<ILaneQuota> quota, string owner)
	{
		_policy = policy;
		_quota = quota;
		_owner = owner;
		_time = time;
		_windows = [.. policy.Limits.Select(limit => new WindowLog(limit))];
		_wake = time.CreateTimer(_ => Pump(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
	}

	public LaneLoad Load(LaneClass asking)
	{
		lock (_gate)
		{
			var now = _time.GetUtcNow();
			var blocked = new[] { _blockedUntil, _downUntil, ReservedUntil(asking, now) }.WhereSome().Where(until => until > now).OrderDescending().FirstOrNone();
			var queued = _windows.Select(window => window.Drain(_waiting.Count, _scale)).DefaultIfEmpty(TimeSpan.Zero).Max();
			return new LaneLoad(blocked, blocked.Map(until => until - now).GetValueOr(TimeSpan.Zero) + queued, IsDown(now), _waiting.Count)
			{
				Paced = Pace(now).When(_waiting.Count > 0 && _inFlight == 0 && blocked.IsNone()),
			};
		}
	}

	public Task<Result<LanePermit>> EnterAsync(LanePriority priority, TimeSpan patience, CancellationToken cancellationToken)
	{
		lock (_gate)
		{
			var now = _time.GetUtcNow();
			return Refusal(priority, now + patience, now)
				.Match<string, Task<Result<LanePermit>>>(
					reason => Task.FromResult<Result<LanePermit>>(Error.From(new LaneRefusedException(reason))),
					() => Enqueue(new Ticket(priority, now + patience), cancellationToken)
				);
		}
	}

	public bool Admits(LanePriority priority, TimeSpan patience)
	{
		lock (_gate)
		{
			var now = _time.GetUtcNow();
			return Refusal(priority, now + patience, now).IsNone();
		}
	}

	internal void Release(AttemptOutcome outcome)
	{
		lock (_gate)
		{
			_inFlight--;
			var now = _time.GetUtcNow();
			switch (outcome)
			{
				case AttemptAnswered:
					Answered(now);
					break;
				case AttemptThrottled throttled:
					Throttled(throttled, now);
					break;
				case AttemptFailed:
					Failed(now);
					break;
				case AttemptAbandoned:
					break;
			}

			PumpLocked();
		}
	}

	public void Dispose() => _wake.Dispose();

	private Task<Result<LanePermit>> Enqueue(Ticket ticket, CancellationToken cancellationToken)
	{
		_waiting.Enqueue(ticket, (ticket.Priority.Rank.Class, ticket.Priority.Rank.Order, _seq++));
		ticket.Watch(cancellationToken, Cancelled);
		PumpLocked();
		return ticket.Task;
	}

	private void Cancelled(Ticket ticket)
	{
		lock (_gate)
		{
			_waiting.Remove(ticket, out _, out _);
			PumpLocked();
		}
	}

	private void Pump()
	{
		lock (_gate)
		{
			PumpLocked();
		}
	}

	private void PumpLocked()
	{
		var now = _time.GetUtcNow();
		while (_waiting.TryPeek(out var head, out _))
		{
			if (head.Task.IsCompleted || Refusal(head.Priority, head.Deadline, now).Map(head.Refuse).GetValueOr(false))
			{
				_waiting.Dequeue();
				continue;
			}

			var openAt = OpenAt(head.Priority, now);
			if (openAt > now)
			{
				_wake.Change(openAt - now < LongestWake ? openAt - now : LongestWake, Timeout.InfiniteTimeSpan);
				return;
			}

			if (_inFlight >= MaxInFlight(now))
			{
				return;
			}

			_waiting.Dequeue();
			if (head.Grant(new LanePermit(this)))
			{
				_inFlight++;
				_probing = _downUntil.IsSome();
				foreach (var window in _windows)
				{
					window.Record(now);
				}
			}
		}
	}

	private int MaxInFlight(DateTimeOffset now) => _probing || _downUntil.Match(until => until <= now, () => false) ? 1 : _policy.MaxInFlight;

	private DateTimeOffset OpenAt(LanePriority priority, DateTimeOffset now)
	{
		return new[] { _blockedUntil.GetValueOr(now), ReservedUntil(priority.Class, now).GetValueOr(now) }
			.Concat(_windows.Select(window => window.NextFree(now, _scale)))
			.Max();
	}

	private Optional<LanePace> Pace(DateTimeOffset now)
	{
		return _windows.Select(window => window.Pace(now, _scale)).Where(pace => pace.OpensAt > now).OrderByDescending(pace => pace.OpensAt).FirstOrNone();
	}

	private Optional<string> Refusal(LanePriority priority, DateTimeOffset deadline, DateTimeOffset now)
	{
		return Optional
			.When(IsDown(now), () => "down")
			.Or(() => _blockedUntil.When(until => until > deadline).Map(until => $"blocked until {until:O}"))
			.Or(() => ReservedUntil(priority.Class, now).When(until => until > deadline).Map(until => $"quota reserve until {until:O}"));
	}

	private bool IsDown(DateTimeOffset now) => _downUntil.Match(until => until > now, () => false);

	private Optional<DateTimeOffset> ReservedUntil(LaneClass asking, DateTimeOffset now)
	{
		return _quota.Map(quota => quota.BlockedUntil(_owner, asking, now));
	}

	private void Answered(DateTimeOffset now)
	{
		_failures = 0;
		_probing = false;
		_downUntil = new None();
		Shortest()
			.When(window => _scale < 1 && now - _throttled >= window.Per && now - _raised >= window.Per)
			.Apply(window => _scale = Math.Min(1, _scale + 1.0 / window.Requests))
			.Apply(_ => _raised = now);
	}

	private void Throttled(AttemptThrottled throttled, DateTimeOffset now)
	{
		_failures = 0;
		_probing = false;
		_throttled = now;
		throttled.RetryAt.Apply(Block).ApplyNone(() => Slow(now));
	}

	private void Slow(DateTimeOffset now)
	{
		Shortest().ApplyNone(() => Block(now + _policy.Pause)).When(window => now - _halved >= window.Per).Apply(window => Halve(window, now));
	}

	private void Halve(RateWindow window, DateTimeOffset now)
	{
		_scale = Math.Max(_scale / 2, 1.0 / window.Requests);
		_halved = now;
	}

	private void Block(DateTimeOffset until)
	{
		_blockedUntil = Some.Of(_blockedUntil.When(current => current > until).GetValueOr(until));
		Refuse(ticket => ticket.Deadline < until, $"blocked until {until:O}");
	}

	private void Failed(DateTimeOffset now)
	{
		_failures++;
		if (_probing || _failures >= _policy.FailuresToDown)
		{
			_downUntil = Some.Of(now + _policy.DownFor);
			_probing = false;
			_failures = 0;
			Refuse(_ => true, "down");
		}
	}

	private void Refuse(Func<Ticket, bool> refused, string reason)
	{
		var kept = _waiting.UnorderedItems.Where(item => !refused(item.Element) || !item.Element.Refuse(reason)).ToList();
		_waiting.Clear();
		_waiting.EnqueueRange(kept);
	}

	private Optional<RateWindow> Shortest() => _policy.Limits.OrderBy(window => window.Per).FirstOrNone();

	private sealed class Ticket(LanePriority priority, DateTimeOffset deadline)
	{
		private readonly TaskCompletionSource<Result<LanePermit>> _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private CancellationTokenRegistration _watch;

		public LanePriority Priority => priority;

		public DateTimeOffset Deadline => deadline;

		public Task<Result<LanePermit>> Task => _done.Task;

		public bool Grant(LanePermit permit) => Settled(_done.TrySetResult(Success.Of(permit)));

		public bool Refuse(string reason) => Settled(_done.TrySetResult(Error.From(new LaneRefusedException(reason))));

		public void Watch(CancellationToken cancellationToken, Action<Ticket> cancelled)
		{
			_watch = cancellationToken.Register(() =>
			{
				if (_done.TrySetCanceled(cancellationToken))
				{
					cancelled(this);
				}
			});
		}

		private bool Settled(bool settled)
		{
			if (settled)
			{
				_watch.Unregister();
			}

			return settled;
		}
	}

	private sealed class WindowLog(RateWindow window)
	{
		private readonly Queue<DateTimeOffset> _granted = new();

		public DateTimeOffset NextFree(DateTimeOffset now, double scale)
		{
			while (_granted.TryPeek(out var oldest) && oldest <= now - window.Per)
			{
				_granted.Dequeue();
			}

			var limit = Limit(scale);
			return window.Aligned ? AlignedFree(now, limit) : SlidingFree(now, limit);
		}

		public void Record(DateTimeOffset now)
		{
			_granted.Enqueue(now);
			while (_granted.Count > window.Requests)
			{
				_granted.Dequeue();
			}
		}

		public TimeSpan Drain(int waiting, double scale) => TimeSpan.FromTicks(window.Per.Ticks * waiting / Limit(scale));

		public LanePace Pace(DateTimeOffset now, double scale) => new(NextFree(now, scale), Limit(scale), window.Per);

		private DateTimeOffset SlidingFree(DateTimeOffset now, int limit)
		{
			return _granted.Count < limit ? now : _granted.ElementAt(_granted.Count - limit) + window.Per;
		}

		private DateTimeOffset AlignedFree(DateTimeOffset now, int limit)
		{
			var start = new DateTimeOffset(now.UtcTicks - now.UtcTicks % window.Per.Ticks, TimeSpan.Zero);
			return _granted.Count(at => at >= start) < limit ? now : start + window.Per;
		}

		private int Limit(double scale) => Math.Max(1, (int)(window.Requests * scale));
	}
}

public sealed class LanePermit
{
	private readonly LaneGate _lane;
	private int _released;

	internal LanePermit(LaneGate lane)
	{
		_lane = lane;
	}

	public void Release(AttemptOutcome outcome)
	{
		if (Interlocked.Exchange(ref _released, 1) == 0)
		{
			_lane.Release(outcome);
		}
	}
}
