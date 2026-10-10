using System.Diagnostics;
using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Media;

namespace Core.Tests.Media;

public class MediaServiceBenchmark
{
	private const int Titles = 2000;
	private const int PageSize = 100;
	private static readonly TimeSpan PageFetch = TimeSpan.FromMilliseconds(50);
	private static readonly TimeSpan Call = TimeSpan.FromMilliseconds(20);
	private static readonly TimeSpan Stall = TimeSpan.FromSeconds(2);

	[Test]
	[Explicit]
	public async Task IdentifyWithDetailsAsync_ForAPagedListWithStallingTitles_ReportsItsMeasures()
	{
		var (testee, deps) = TestMediaService.Create();
		var lookups = new Concurrency();
		var details = new Concurrency();
		var repository = 0;
		deps.Converter.LookUpAsync(Any(), Any())
			.ReturnsAsync(
				async (request, token) =>
				{
					using var _ = lookups.Enter();
					await Task.Delay(Stalls(request) ? Stall : Call, token);
					return TestMediaService.FoundByFind(request);
				}
			);
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any())
			.ReturnsAsync(
				async (ext, token) =>
				{
					using var _ = details.Enter();
					await Task.Delay(Call, token);
					return Success.Of(TestMediaService.CompleteMovie(ext));
				}
			);
		deps.MediaRepo.GetByExternalsAsync(Any(), Any())
			.Returns(
				(_, _) =>
				{
					Interlocked.Increment(ref repository);
					return [];
				}
			);
		deps.MediaRepo.FindIdentitiesAsync(Any(), Any())
			.Returns(
				(groups, _) =>
				{
					Interlocked.Increment(ref repository);
					return [.. Enumerable.Repeat<Optional<MediaIdentity>>(new None(), groups.Count)];
				}
			);
		var clock = Stopwatch.StartNew();
		var first = TimeSpan.Zero;
		var half = TimeSpan.Zero;
		var handed = 0;

		await foreach (var _ in testee.IdentifyWithDetailsAsync(PagedAsync()))
		{
			handed++;
			if (handed == 1)
			{
				first = clock.Elapsed;
			}

			if (handed == Titles / 2)
			{
				half = clock.Elapsed;
			}
		}

		Console.WriteLine($"wall: {clock.Elapsed.TotalMilliseconds:F0} ms");
		Console.WriteLine($"first result: {first.TotalMilliseconds:F0} ms");
		Console.WriteLine($"half out: {half.TotalMilliseconds:F0} ms");
		Console.WriteLine($"peak lookups: {lookups.Peak}");
		Console.WriteLine($"peak details calls: {details.Peak}");
		Console.WriteLine($"repository calls: {Volatile.Read(ref repository)}");
		await Assert.That(handed).IsEqualTo(Titles);
	}

	private static bool Stalls(IdLookupRequest request)
	{
		return request switch
		{
			ByIdRequest(_, var id) => id.Value.EndsWith("37", StringComparison.Ordinal),
			ByHintsRequest => false,
		};
	}

	private static async IAsyncEnumerable<IdentifyRequest> PagedAsync()
	{
		for (var page = 0; page < Titles / PageSize; page++)
		{
			await Task.Delay(PageFetch);
			foreach (var index in Enumerable.Range(page * PageSize, PageSize))
			{
				yield return new IdentifyRequest([ImdbId.From($"tt{index + 1}", MediaType.Movie)], new None());
			}
		}
	}

	private sealed class Concurrency
	{
		private int _current;
		private int _peak;

		internal int Peak => Volatile.Read(ref _peak);

		internal IDisposable Enter()
		{
			var now = Interlocked.Increment(ref _current);
			for (var seen = Volatile.Read(ref _peak); now > seen; seen = Volatile.Read(ref _peak))
			{
				Interlocked.CompareExchange(ref _peak, now, seen);
			}
			return new Exit(this);
		}

		private sealed class Exit(Concurrency owner) : IDisposable
		{
			public void Dispose() => Interlocked.Decrement(ref owner._current);
		}
	}
}
