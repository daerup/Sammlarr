using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Shared.Extensions;

public static class AsyncEnumerableExtensions
{
	extension<TIn>(IAsyncEnumerable<TIn> source)
	{
		public IAsyncEnumerable<TOut> SelectAwait<TOut>(Func<TIn, ValueTask<TOut>> predicate) =>
			source.Select(async (TIn @in, CancellationToken _) => await predicate(@in));

		public async IAsyncEnumerable<TOut> SelectAwait<TOut>(
			Func<TIn, ValueTask<TOut>> selector,
			int maxConcurrency,
			[EnumeratorCancellation] CancellationToken cancellationToken = default
		)
		{
			var anchor = Activity.Current;
			var channel = Channel.CreateUnbounded<Task<TOut>>();
			var sem = new SemaphoreSlim(maxConcurrency, maxConcurrency);

			var producer = Task.Run(
				async () =>
				{
					Activity.Current = anchor;
					try
					{
						await foreach (var item in source.WithCancellation(cancellationToken))
						{
							await sem.WaitAsync(cancellationToken);
							await channel.Writer.WriteAsync(RunAndRelease(item), cancellationToken);
						}
					}
					finally
					{
						channel.Writer.Complete();
					}
				},
				cancellationToken
			);

			await foreach (var task in channel.Reader.ReadAllAsync(cancellationToken))
			{
				Activity.Current = anchor;
				yield return await task;
			}

			await producer;
			yield break;

			async Task<TOut> RunAndRelease(TIn item)
			{
				Activity.Current = anchor;
				try
				{
					return await selector(item);
				}
				finally
				{
					sem.Release();
				}
			}
		}

		public IAsyncEnumerable<TIn> PrefetchAsync(int capacity, CancellationToken cancellationToken = default) =>
			Channel
				.CreateBounded<TIn>(capacity)
				.StreamAsync(
					async (writer, token) =>
					{
						await foreach (var item in source.WithCancellation(token))
						{
							await writer.WriteAsync(item, token);
						}
					},
					cancellationToken
				);

		public async IAsyncEnumerable<(TIn First, TOther Second)> ZipAwait<TOther>(
			IEnumerable<TOther> other,
			[EnumeratorCancellation] CancellationToken cancellationToken = default
		)
		{
			using var otherEnumerator = other.GetEnumerator();
			await foreach (var item in source.WithCancellation(cancellationToken))
			{
				if (!otherEnumerator.MoveNext())
				{
					yield break;
				}
				yield return (item, otherEnumerator.Current);
			}
		}
	}

	extension<TIn>(IEnumerable<TIn> source)
	{
		public IAsyncEnumerable<TOut> SelectAwait<TOut>(Func<TIn, ValueTask<TOut>> predicate, int maxConcurrency, CancellationToken cancellationToken = default) =>
			source.ToAsyncEnumerable().SelectAwait(predicate, maxConcurrency, cancellationToken);
	}

	extension<T>(Channel<T> channel)
	{
		public async IAsyncEnumerable<T> StreamAsync(
			Func<ChannelWriter<T>, CancellationToken, Task> produce,
			[EnumeratorCancellation] CancellationToken cancellationToken = default
		)
		{
			var anchor = Activity.Current;
			using var abandon = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

			var producer = Task.Run(async () =>
			{
				Activity.Current = anchor;
				try
				{
					await produce(channel.Writer, abandon.Token);
					channel.Writer.Complete();
				}
				catch (Exception ex)
				{
					channel.Writer.Complete(ex);
				}
			});

			try
			{
				await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
				{
					Activity.Current = anchor;
					yield return item;
				}
			}
			finally
			{
				await abandon.CancelAsync();
				await producer;
			}
		}
	}
}
