namespace Shared;

public static class AsyncEnumerableExtensions
{
	extension<TIn>(IAsyncEnumerable<TIn> source)
	{
		public IAsyncEnumerable<TOut> SelectAwait<TOut>(Func<TIn, ValueTask<TOut>> predicate)
		{
			return source.Select(async (TIn @in, CancellationToken _) => await predicate(@in));
		}
	}
}
