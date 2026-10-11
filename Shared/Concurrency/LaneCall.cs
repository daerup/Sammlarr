namespace Shared.Concurrency;

public sealed record LaneCall<TKey, TOut>(
	Func<TKey, string> BatchKey,
	Func<IReadOnlyList<TKey>, CancellationToken, Task<Result<IReadOnlyDictionary<TKey, TOut>>>> SendAsync
)
	where TKey : notnull
	where TOut : notnull;

public interface ILane<TKey, TOut>
	where TKey : notnull
	where TOut : notnull
{
	Task<LaneAnswer<TOut>> AskAsync(TKey key, LaneCall<TKey, TOut> call, CancellationToken cancellationToken);

	IReadOnlyList<Task<LaneAnswer<TOut>>> AskMany(IReadOnlyList<TKey> keys, LaneCall<TKey, TOut> call, CancellationToken cancellationToken);

	Optional<Task<LaneAnswer<TOut>>> Join(TKey key, CancellationToken cancellationToken);
}
