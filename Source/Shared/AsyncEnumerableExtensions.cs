namespace Shared;

public static class AsyncEnumerableExtensions
{
	extension<TIn>(IAsyncEnumerable<TIn> source)
	{
		public IAsyncEnumerable<TOut> SelectAwait<TOut>(Func<TIn, ValueTask<TOut>> predicate)
		{
			return source.Select(async (TIn @in, CancellationToken _) => await predicate(@in));
		}

		public async Task<List<TOut>> SelectAwait<TOut>(Func<TIn, ValueTask<TOut>> predicate, int concurrent, CancellationToken cancellationToken = default)
		{
			var results = new System.Collections.Concurrent.ConcurrentBag<TOut>();
			await Parallel.ForEachAsync(
				source,
				new ParallelOptions { MaxDegreeOfParallelism = concurrent, CancellationToken = cancellationToken },
				async (item, _) =>
				{
					results.Add(await predicate(item));
				}
			);

			return results.ToList();
		}
	}
}
