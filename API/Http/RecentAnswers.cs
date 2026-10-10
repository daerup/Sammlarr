using Contracts;

namespace API.Http;

internal sealed class RecentAnswers<TKey, TValue>(IDateTimeProvider clock)
	where TKey : notnull
	where TValue : notnull
{
	private static readonly TimeSpan Kept = TimeSpan.FromMinutes(10);

	private readonly Lock _gate = new();

	private readonly Dictionary<TKey, Filed> _answers = [];

	public Task<Result<TValue>> AskAsync(TKey key, Func<Task<Result<TValue>>> ask, CancellationToken cancellationToken = default)
	{
		Lazy<Task<Result<TValue>>> answer;
		lock (_gate)
		{
			answer = _answers.GetValueOrNone(key).When(Fresh).Match(filed => filed.Answer, () => Start(key, ask));
		}

		return answer.Value.WaitAsync(cancellationToken);
	}

	public void Remember(TKey key, TValue value)
	{
		lock (_gate)
		{
			_answers[key] = new Filed(new Lazy<Task<Result<TValue>>>(Task.FromResult<Result<TValue>>(Success.Of(value))), clock.Now);
		}
	}

	private bool Fresh(Filed filed) => clock.Now - filed.At < Kept;

	private Lazy<Task<Result<TValue>>> Start(TKey key, Func<Task<Result<TValue>>> ask)
	{
		Filed filed = default;
		filed = new Filed(new Lazy<Task<Result<TValue>>>(() => KeepAsync(key, filed, ask)), clock.Now);
		_answers[key] = filed;
		return filed.Answer;
	}

	private async Task<Result<TValue>> KeepAsync(TKey key, Filed filed, Func<Task<Result<TValue>>> ask)
	{
		try
		{
			return (await ask()).ApplyError(_ => Forget(key, filed));
		}
		catch (Exception)
		{
			Forget(key, filed);
			throw;
		}
	}

	private void Forget(TKey key, Filed filed)
	{
		lock (_gate)
		{
			_answers.GetValueOrNone(key).When(stored => stored == filed).Apply(_ => _answers.Remove(key));
		}
	}

	private readonly record struct Filed(Lazy<Task<Result<TValue>>> Answer, DateTimeOffset At);
}
