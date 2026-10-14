using Contracts;

namespace API.Http;

internal sealed class RecentAnswers<TKey, TValue>(IDateTimeProvider clock)
	where TKey : notnull
	where TValue : notnull
{
	private static readonly TimeSpan Kept = TimeSpan.FromMinutes(10);

	private readonly Lock _gate = new();

	private readonly Dictionary<TKey, Filed> _answers = [];

	public Task<Result<TValue>> AskAsync(TKey key, Func<CancellationToken, Task<Result<TValue>>> ask, CancellationToken cancellationToken = default)
	{
		if (cancellationToken.IsCancellationRequested)
		{
			return Task.FromCanceled<Result<TValue>>(cancellationToken);
		}

		Filed filed;
		lock (_gate)
		{
			filed = _answers.GetValueOrNone(key).When(Fresh).GetValueOr(() => Start(key, ask));
			filed.Join(() => Leave(key, filed), cancellationToken);
		}

		return filed.Fetching.IsCancellationRequested ? Task.FromCanceled<Result<TValue>>(filed.Fetching) : filed.Answer.Value.WaitAsync(cancellationToken);
	}

	public void Remember(TKey key, TValue value)
	{
		lock (_gate)
		{
			var filed = new Filed(clock.Now, _ => Task.FromResult<Result<TValue>>(Success.Of(value)));
			filed.Settle();
			_answers[key] = filed;
		}
	}

	private bool Fresh(Filed filed) => clock.Now - filed.At < Kept;

	private Filed Start(TKey key, Func<CancellationToken, Task<Result<TValue>>> ask)
	{
		var filed = new Filed(clock.Now, started => KeepAsync(key, started, ask));
		_answers[key] = filed;
		return filed;
	}

	private async Task<Result<TValue>> KeepAsync(TKey key, Filed filed, Func<CancellationToken, Task<Result<TValue>>> ask)
	{
		try
		{
			return (await ask(filed.Fetching)).ApplyError(_ => Forget(key, filed));
		}
		catch (Exception)
		{
			Forget(key, filed);
			throw;
		}
		finally
		{
			lock (_gate)
			{
				filed.Settle();
			}
		}
	}

	private void Leave(TKey key, Filed filed)
	{
		bool last;
		lock (_gate)
		{
			last = filed.LeaveLast();
			if (last)
			{
				Forget(key, filed);
			}
		}

		if (last)
		{
			filed.Cancel();
		}
	}

	private void Forget(TKey key, Filed filed)
	{
		lock (_gate)
		{
			_answers.GetValueOrNone(key).When(stored => stored == filed).Apply(_ => _answers.Remove(key));
		}
	}

	private sealed class Filed
	{
		private readonly CancellationTokenSource _fetching = new();

		private readonly List<CancellationTokenRegistration> _waiters = [];

		private int _waiting;

		private bool _settled;

		public Filed(DateTimeOffset at, Func<Filed, Task<Result<TValue>>> start)
		{
			At = at;
			Answer = new Lazy<Task<Result<TValue>>>(() => start(this));
		}

		public DateTimeOffset At { get; }

		public Lazy<Task<Result<TValue>>> Answer { get; }

		public CancellationToken Fetching => _fetching.Token;

		public void Join(Action leave, CancellationToken waiter)
		{
			if (_settled)
			{
				return;
			}

			_waiting++;
			_waiters.Add(waiter.Register(leave));
		}

		public bool LeaveLast()
		{
			_waiting--;
			return _waiting == 0 && !_settled;
		}

		public void Cancel() => _fetching.Cancel();

		public void Settle()
		{
			_settled = true;
			_waiters.ForEach(waiter => waiter.Unregister());
			_waiters.Clear();
		}
	}
}
