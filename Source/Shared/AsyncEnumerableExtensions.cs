using System.Diagnostics;
using System.Runtime.CompilerServices;
using Contracts.Media.Convertions;
using Contracts.Media.Metadata;
using Contracts.Watchlist;

namespace Shared;

public static class AsyncEnumerableExtensions
{
	extension<TIn>(IAsyncEnumerable<TIn> source)
	{
		public IAsyncEnumerable<TOut> SelectAwait<TOut>(Func<TIn, ValueTask<TOut>> predicate) =>
			source.Select(async (TIn @in, CancellationToken _) => await predicate(@in));

		public async IAsyncEnumerable<TOut> SelectAwait<TOut>(
			Func<TIn, ValueTask<TOut>> predicate,
			int maxConcurrency,
			[EnumeratorCancellation] CancellationToken cancellationToken = default
		)
		{
			var anchor = Activity.Current;
			var window = new Queue<Task<TOut>>(maxConcurrency);
			await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

			while (window.Count < maxConcurrency && await enumerator.MoveNextAsync())
			{
				Activity.Current = anchor;
				window.Enqueue(predicate(enumerator.Current).AsTask());
			}

			while (window.Count > 0)
			{
				yield return await window.Dequeue();
				if (await enumerator.MoveNextAsync())
				{
					Activity.Current = anchor;
					window.Enqueue(predicate(enumerator.Current).AsTask());
				}
			}
		}
	}
}

public static class MediaMetadataProviderExtensions
{
	extension(IMediaMetadataProvider)
	{
		public static int RecommendedConcurrency => 50;
	}
}

public static class MediaIdResolverExtensions
{
	extension(IMediaIdResolver)
	{
		public static int RecommendedConcurrency => 50;
	}
}

public static class WatchlistServiceExtensions
{
	extension(IWatchlistService)
	{
		public static int RecommendedConcurrency => 10;
	}
}
