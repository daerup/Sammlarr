namespace Shared.Concurrency;

public sealed class Lane<TKey, TOut>(int maxBatch) : ILane<TKey, TOut>
	where TKey : notnull
	where TOut : notnull
{
	private readonly Lock _gate = new();
	private readonly Dictionary<(TKey Key, LaneClass Class), Pending> _pending = [];
	private readonly Dictionary<(string Batch, LaneClass Class), Batches> _batches = [];
	private long _seq;

	private int MaxInFlight => maxBatch == 1 ? int.MaxValue : 1;

	public Task<LaneAnswer<TOut>> AskAsync(TKey key, LaneCall<TKey, TOut> call, CancellationToken cancellationToken)
	{
		var priority = LanePriority.Current;
		lock (_gate)
		{
			var pending = _pending.GetValueOrNone((key, priority.Class)).GetValueOr(() => Queue(key, call, priority));
			var answer = pending.JoinAsync(cancellationToken);
			Drain((pending.Call.BatchKey(key), priority.Class));
			return answer;
		}
	}

	public IReadOnlyList<Task<LaneAnswer<TOut>>> AskMany(IReadOnlyList<TKey> keys, LaneCall<TKey, TOut> call, CancellationToken cancellationToken)
	{
		var priority = LanePriority.Current;
		lock (_gate)
		{
			List<Task<LaneAnswer<TOut>>> answers =
			[
				.. keys.Select(key => _pending.GetValueOrNone((key, priority.Class)).GetValueOr(() => Queue(key, call, priority)).JoinAsync(cancellationToken)),
			];
			foreach (var batch in keys.Select(key => (call.BatchKey(key), priority.Class)).Distinct().ToList())
			{
				Drain(batch);
			}

			return answers;
		}
	}

	public Optional<Task<LaneAnswer<TOut>>> Join(TKey key, CancellationToken cancellationToken)
	{
		var asking = LanePriority.Current.Class;
		lock (_gate)
		{
			return Enum.GetValues<LaneClass>()
				.OrderByDescending(each => each == asking)
				.Select(each => _pending.GetValueOrNone((key, each)))
				.WhereSome()
				.FirstOrNone()
				.Map(pending => new Some<Task<LaneAnswer<TOut>>>(pending.JoinAsync(cancellationToken)));
		}
	}

	private Pending Queue(TKey key, LaneCall<TKey, TOut> call, LanePriority priority)
	{
		var pending = new Pending(key, call, priority, Optional.Of(ExecutionContext.Capture()));
		_pending[(key, priority.Class)] = pending;
		var batch = (call.BatchKey(key), priority.Class);
		var batches = _batches.GetValueOrNone(batch).GetValueOr(() => _batches[batch] = new Batches());
		batches.Waiting.Enqueue(pending, (priority.Rank.Class, priority.Rank.Order, _seq++));
		return pending;
	}

	private void Drain((string Batch, LaneClass Class) batch)
	{
		var batches = _batches[batch];
		while (batches.InFlight < MaxInFlight && Take(batches) is { Count: > 0 } taken)
		{
			batches.InFlight++;
			_ = SendAsync(batches, batch, taken);
		}
	}

	private List<Pending> Take(Batches batches)
	{
		var taken = new List<Pending>();
		while (taken.Count < maxBatch && batches.Waiting.TryDequeue(out var next, out _))
		{
			if (next.Abandoned)
			{
				_pending.Remove((next.Key, next.Priority.Class));
				continue;
			}

			taken.Add(next);
		}

		return taken;
	}

	private async Task SendAsync(Batches batches, (string Batch, LaneClass Class) batch, List<Pending> taken)
	{
		var asking = Sending(taken[0], [.. taken.Select(pending => pending.Key)]);
		await ((Task)asking).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
		var answer = Settled(asking);
		lock (_gate)
		{
			foreach (var pending in taken)
			{
				_pending.Remove((pending.Key, pending.Priority.Class));
				pending.Settle(Answer(answer, pending.Key));
			}

			batches.InFlight--;
			Drain(batch);
		}
	}

	private static Task<Result<IReadOnlyDictionary<TKey, TOut>>> Sending(Pending oldest, IReadOnlyList<TKey> keys)
	{
		var send = () => Task.Run(() => oldest.Call.SendAsync(keys, CancellationToken.None));
		return oldest.Context.Match<ExecutionContext, Task<Result<IReadOnlyDictionary<TKey, TOut>>>>(context => Within(context, send), send);
	}

	private static Task<T> Within<T>(ExecutionContext context, Func<Task<T>> start)
	{
		Task<T>? started = null;
		ExecutionContext.Run(context, _ => started = start(), null);
		return started!;
	}

	private static LaneAnswer<TOut> Answer(Result<IReadOnlyDictionary<TKey, TOut>> answer, TKey key)
	{
		return answer.Match<IReadOnlyDictionary<TKey, TOut>, LaneAnswer<TOut>>(
			found => found.GetValueOrNone(key).Match<TOut, LaneAnswer<TOut>>(value => new LaneHit<TOut>(value), () => new LaneMiss()),
			error => new LaneFailed(error)
		);
	}

	private static Result<IReadOnlyDictionary<TKey, TOut>> Settled(Task<Result<IReadOnlyDictionary<TKey, TOut>>> asking)
	{
		return asking.IsCompletedSuccessfully
			? asking.Result
			: Optional.Of(asking.Exception).Map(failed => Error.From(failed.GetBaseException())).GetValueOr(() => Error.From("the batch was cancelled"));
	}

	private sealed class Batches
	{
		public PriorityQueue<Pending, (int Class, long Order, long Seq)> Waiting { get; } = new();

		public int InFlight { get; set; }
	}

	private sealed class Pending(TKey key, LaneCall<TKey, TOut> call, LanePriority priority, Optional<ExecutionContext> context)
	{
		private readonly TaskCompletionSource<LaneAnswer<TOut>> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly List<CancellationTokenRegistration> _askers = [];
		private int _asking;

		public TKey Key => key;

		public LaneCall<TKey, TOut> Call => call;

		public LanePriority Priority => priority;

		public Optional<ExecutionContext> Context => context;

		public bool Abandoned => Volatile.Read(ref _asking) == 0;

		public Task<LaneAnswer<TOut>> JoinAsync(CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref _asking);
			_askers.Add(cancellationToken.Register(() => Interlocked.Decrement(ref _asking)));
			return _answer.Task.WaitAsync(cancellationToken);
		}

		public void Settle(LaneAnswer<TOut> answer)
		{
			_askers.ForEach(asker => asker.Unregister());
			_answer.TrySetResult(answer);
		}
	}
}
