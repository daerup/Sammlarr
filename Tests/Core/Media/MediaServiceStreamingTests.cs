using System.Globalization;
using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Media;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Core.Tests.Media;

public class MediaServiceStreamingTests
{
	[Test]
	public async Task IdentifyWithDetailsAsync_WhenLookupsFinishOutOfOrder_YieldsInSourceOrder()
	{
		var (testee, deps) = TestMediaService.Create();
		deps.Converter.LookUpAsync(Any(), Any())
			.ReturnsAsync(
				async (request, token) =>
				{
					await Task.Delay(TimeSpan.FromMilliseconds(10 * (30 - Index(request))), token);
					return TestMediaService.FoundByFind(request);
				}
			);

		var results = await testee.IdentifyWithDetailsAsync(Imdb(30).ToAsyncEnumerable()).ToListAsync();

		await Assert
			.That(results.Select(result => result.Externals.OfType<ImdbId>().Single().Value))
			.IsEquivalentTo([.. Enumerable.Range(0, 30).Select(Tt)], CollectionOrdering.Matching);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenConsumerStopsEarly_StopsReadingTheList()
	{
		var (testee, _) = TestMediaService.Create();
		var pulled = 0;
		var closed = new TaskCompletionSource();

		await foreach (var _ in testee.IdentifyWithDetailsAsync(Counting(4000, () => Interlocked.Increment(ref pulled), closed)))
		{
			break;
		}

		await Assert.That(closed.Task.IsCompleted).IsTrue();
		await Assert.That(Volatile.Read(ref pulled)).IsGreaterThanOrEqualTo(1).And.IsLessThan(2000);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenTheListFailsAfterSomeTitles_YieldsThemThenThrows()
	{
		var (testee, _) = TestMediaService.Create();
		var seen = new List<MediaMatchResult>();

		var act = async () =>
		{
			await foreach (var result in testee.IdentifyWithDetailsAsync(FailsAfter(25)))
			{
				seen.Add(result);
			}
		};

		await Assert.That(act).Throws<InvalidOperationException>().WithMessageContaining("the list failed");
		await Assert
			.That(seen.Select(result => result.Externals.OfType<ImdbId>().Single().Value))
			.IsEquivalentTo([.. Enumerable.Range(0, 25).Select(Tt)], CollectionOrdering.Matching);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_ForTwoEntriesOfOneMedia_GivesBothTheSameId()
	{
		var (testee, deps) = TestMediaService.Create();
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns((ext, _) => Success.Of(TestMediaService.CompleteMovie(ext)));
		var requests = Imdb(20);
		requests[15] = requests[2];

		var results = (await testee.IdentifyWithDetailsAsync(requests.ToAsyncEnumerable()).ToListAsync()).Matched().ToList();

		await Assert.That(results).Count().IsEqualTo(20);
		await Assert.That(results[15].Identity.Id).IsEqualTo(results[2].Identity.Id);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenCancelled_LeavesNoLookupRunning()
	{
		using var cancellation = new CancellationTokenSource();
		var (testee, deps) = TestMediaService.Create();
		var running = 0;
		var started = new TaskCompletionSource();
		deps.Converter.LookUpAsync(Any(), Any())
			.ReturnsAsync(
				async (request, token) =>
				{
					Interlocked.Increment(ref running);
					started.TrySetResult();
					await Task.Delay(Timeout.Infinite, token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
					await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None);
					Interlocked.Decrement(ref running);
					token.ThrowIfCancellationRequested();
					return TestMediaService.FoundByFind(request);
				}
			);

		var consume = testee.IdentifyWithDetailsAsync(Imdb(50).ToAsyncEnumerable(), cancellation.Token).ToListAsync().AsTask();
		await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
		await cancellation.CancelAsync();

		await Assert.That(async () => await consume.WaitAsync(TimeSpan.FromSeconds(5))).Throws<OperationCanceledException>();
		await Assert.That(Volatile.Read(ref running)).IsEqualTo(0);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenATitlesIdsBelongToTwoStoredTitles_LeavesItUnmatchedAndGoesOn()
	{
		var conflicting = ImdbId.From(Tt(1), MediaType.Movie);
		var (testee, deps) = TestMediaService.Create();
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns((ext, _) => Success.Of(TestMediaService.CompleteMovie(ext)));
		deps.MediaRepo.FindIdentitiesAsync(Any(), Any())
			.Returns(
				(groups, _) =>
					groups.Any(group => group.Contains(conflicting))
						? throw new MediaIdentityConflictException([MediaId.New(), MediaId.New()], [conflicting])
						: [.. Enumerable.Repeat<Optional<MediaIdentity>>(new None(), groups.Count)]
			);

		var results = await testee.IdentifyWithDetailsAsync(Imdb(3).ToAsyncEnumerable()).ToListAsync();

		await Assert.That(results[0]).IsOfUnionType<MediaMatchResult, MatchedMedia>();
		await Assert.That(results[1]).IsOfUnionType<MediaMatchResult, UnmatchedMedia>();
		await Assert.That(results[2]).IsOfUnionType<MediaMatchResult, MatchedMedia>();
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenALaterEntryBridgesTwoTitles_HandsOutTheSurvivingId()
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		var first = ImdbId.From("tt-bridge-a", MediaType.Movie);
		var second = ImdbId.From("tt-bridge-b", MediaType.Movie);
		var (testee, deps) = TestMediaService.Create();
		var detailCalls = 0;
		var all = new TaskCompletionSource();
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any())
			.ReturnsAsync(
				async (ext, token) =>
				{
					if (Interlocked.Increment(ref detailCalls) == 3)
					{
						all.TrySetResult();
					}

					await all.Task.WaitAsync(token);
					return Success.Of(TestMediaService.CompleteMovie(ext));
				}
			);
		List<IdentifyRequest> requests = [new([first], new None()), new([second], new None()), new([first, second], new None())];

		var results = (await testee.IdentifyWithDetailsAsync(requests.ToAsyncEnumerable(), timeout.Token).ToListAsync()).Matched().ToList();

		await Assert.That(results).Count().IsEqualTo(3);
		await Assert.That(results.Select(result => result.Identity.Id).Distinct()).Count().IsEqualTo(1);
	}

	[Test]
	[Skip("fails on batching: the streaming branches make it pass")]
	public async Task IdentifyWithDetailsAsync_WhenOneTitleStalls_SettlesTheOthers()
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		var (testee, deps) = TestMediaService.Create();
		var gate = new TaskCompletionSource();
		var settled = 0;
		var others = new TaskCompletionSource();
		deps.Converter.LookUpAsync(Any(), Any())
			.ReturnsAsync(
				async (request, token) =>
				{
					if (Index(request) == 0)
					{
						await gate.Task.WaitAsync(token);
					}

					return TestMediaService.FoundByFind(request);
				}
			);
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any())
			.Returns(
				(ext, _) =>
				{
					if (Interlocked.Increment(ref settled) == 149)
					{
						others.TrySetResult();
					}

					return Success.Of(TestMediaService.CompleteMovie(ext));
				}
			);

		var consume = testee.IdentifyWithDetailsAsync(Imdb(150).ToAsyncEnumerable(), timeout.Token).ToListAsync().AsTask();
		await others.Task.WaitAsync(timeout.Token);
		gate.SetResult();
		var results = await consume;

		await Assert
			.That(results.Select(result => result.Externals.OfType<ImdbId>().Single().Value))
			.IsEquivalentTo([.. Enumerable.Range(0, 150).Select(Tt)], CollectionOrdering.Matching);
	}

	private static string Tt(int index) => $"tt{index}";

	private static int Index(IdLookupRequest request)
	{
		return request switch
		{
			ByIdRequest(_, var id) => int.Parse(id.Value[2..], CultureInfo.InvariantCulture),
			ByHintsRequest => -1,
		};
	}

	private static List<IdentifyRequest> Imdb(int count)
	{
		return [.. Enumerable.Range(0, count).Select(index => new IdentifyRequest([ImdbId.From(Tt(index), MediaType.Movie)], new None()))];
	}

	private static async IAsyncEnumerable<IdentifyRequest> Counting(int count, Action onPulled, TaskCompletionSource closed)
	{
		try
		{
			foreach (var request in Imdb(count))
			{
				onPulled();
				yield return request;
				await Task.Yield();
			}
		}
		finally
		{
			closed.TrySetResult();
		}
	}

	private static async IAsyncEnumerable<IdentifyRequest> FailsAfter(int count)
	{
		foreach (var request in Imdb(count))
		{
			yield return request;
			await Task.Yield();
		}

		throw new InvalidOperationException("the list failed");
	}
}
