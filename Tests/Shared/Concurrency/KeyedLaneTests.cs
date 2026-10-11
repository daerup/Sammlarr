using Shared.Concurrency;
using TestInfrastructure;

namespace Shared.Tests.Concurrency;

public class KeyedLaneTests
{
	private static readonly AsyncLocal<string> Asker = new();

	private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

	[Test]
	public async Task AskAsync_WhileABatchIsInFlight_GathersTheNextBatch()
	{
		var open = new TaskCompletionSource();
		var sent = new List<SentBatch>();
		var testee = new Lane<int, int>(1000);
		var call = Echo(sent, _ => "imdb/movie", _ => open.Task);

		var answers = Enumerable.Range(0, 4000).Select(key => testee.AskAsync(key, call, CancellationToken.None)).ToList();
		open.SetResult();
		await Task.WhenAll(answers).WaitAsync(Patience);

		await Assert.That(sent.Select(batch => batch.Keys.Count)).IsEquivalentTo([1, 1000, 1000, 1000, 999]);
	}

	[Test]
	public async Task AskAsync_WhenTheBatchFails_SettlesEveryKeyAsFailedOnce()
	{
		var open = new TaskCompletionSource();
		var calls = 0;
		var testee = new Lane<int, int>(200);
		var call = new LaneCall<int, int>(
			_ => "imdb/movie",
			async (_, _) =>
			{
				Interlocked.Increment(ref calls);
				await open.Task;
				return Error.From("MDBList answered 502");
			}
		);

		var answers = Enumerable.Range(0, 150).Select(key => testee.AskAsync(key, call, CancellationToken.None)).ToList();
		open.SetResult();

		await Assert.That(await Task.WhenAll(answers).WaitAsync(Patience)).All().Satisfy(answer => answer.IsOfUnionType<LaneAnswer<int>, LaneFailed>());
		await Assert.That(calls).IsEqualTo(2);
	}

	[Test]
	public async Task AskAsync_WhenTheSendThrows_SettlesTheKeyAsFailed()
	{
		var testee = new Lane<int, int>(10);
		var call = new LaneCall<int, int>(_ => "imdb/movie", (_, _) => throw new InvalidOperationException("The client is gone."));

		var answer = await testee.AskAsync(1, call, CancellationToken.None);

		await Assert.That(answer).IsOfUnionType<LaneAnswer<int>, LaneFailed>();
	}

	[Test]
	public async Task AskAsync_ForAKeyMissingFromTheAnswer_IsAMiss()
	{
		var testee = new Lane<int, int>(10);
		var call = new LaneCall<int, int>(
			_ => "imdb/movie",
			(_, _) => Task.FromResult<Result<IReadOnlyDictionary<int, int>>>(Success.Of<IReadOnlyDictionary<int, int>>(new Dictionary<int, int> { [1] = 1 }))
		);

		var answer = await testee.AskAsync(2, call, CancellationToken.None);

		await Assert.That(answer).IsOfUnionType<LaneAnswer<int>, LaneMiss>();
	}

	[Test]
	public async Task AskAsync_WhenEveryAskerCancels_DoesNotSendTheKey()
	{
		var open = new TaskCompletionSource();
		var sent = new List<SentBatch>();
		var testee = new Lane<int, int>(10);
		var call = Echo(sent, _ => "imdb/movie", _ => open.Task);
		var first = testee.AskAsync(1, call, CancellationToken.None);
		using var cancellation = new CancellationTokenSource();
		var abandoned = testee.AskAsync(2, call, cancellation.Token);

		await cancellation.CancelAsync();
		open.SetResult();
		await first.WaitAsync(Patience);

		await Assert.That(async () => await abandoned).Throws<OperationCanceledException>();
		await Assert.That(sent.SelectMany(batch => batch.Keys)).IsEquivalentTo([1]);
	}

	[Test]
	public async Task AskAsync_ForTwoBatchKeys_NeverMixesThem()
	{
		var sent = new List<SentBatch>();
		var testee = new Lane<int, int>(50);
		var call = Echo(sent, key => key % 2 == 0 ? "even" : "odd", _ => Task.Delay(5));

		await Task.WhenAll(Enumerable.Range(0, 400).Select(key => Task.Run(() => testee.AskAsync(key, call, CancellationToken.None)))).WaitAsync(Patience);

		await Assert.That(sent.Where(batch => batch.Keys.Select(key => key % 2).Distinct().Count() > 1)).IsEmpty();
	}

	[Test]
	public async Task AskAsync_ForTitleAndBackgroundKeysOfOneBatchKey_SendsThemInSeparateBatches()
	{
		var sent = new List<SentBatch>();
		var testee = new Lane<int, int>(50);
		var call = Echo(sent, _ => "imdb/movie", _ => Task.Delay(5));

		await Task.WhenAll([.. Enumerable.Range(0, 200).Select(key => AskAs(testee, key, call, key % 2 == 0 ? LaneClass.Title : LaneClass.Background))])
			.WaitAsync(Patience);

		await Assert.That(sent.Where(batch => batch.Keys.Any(key => key % 2 != (batch.Class is LaneClass.Title ? 0 : 1)))).IsEmpty();
	}

	[Test]
	public async Task AskAsync_ForAKeyAskedByTitleWhileABackgroundAskWaits_SendsItInATitleBatch()
	{
		var backgroundOpen = new TaskCompletionSource();
		var sent = new List<SentBatch>();
		var testee = new Lane<int, int>(10);
		var call = Echo(sent, _ => "imdb/movie", asking => asking is LaneClass.Title ? Task.CompletedTask : backgroundOpen.Task);
		var inFlight = AskAs(testee, 1, call, LaneClass.Background);
		var waiting = AskAs(testee, 7, call, LaneClass.Background);

		var answer = await AskAs(testee, 7, call, LaneClass.Title).WaitAsync(Patience);
		var backgroundStillWaiting = !waiting.IsCompleted;
		backgroundOpen.SetResult();
		await Task.WhenAll(inFlight, waiting).WaitAsync(Patience);

		await Assert.That(answer).IsOfUnionType<LaneAnswer<int>, LaneHit<int>>();
		await Assert.That(backgroundStillWaiting).IsTrue();
		await Assert.That(sent.Single(batch => batch.Class is LaneClass.Title).Keys).IsEquivalentTo([7]);
	}

	[Test]
	public async Task AskAsync_ForABatch_SendsItInTheOldestAskersContext()
	{
		var open = new TaskCompletionSource();
		var sent = new List<SentBatch>();
		var testee = new Lane<int, int>(10);
		var call = Echo(sent, _ => "imdb/movie", _ => open.Task);

		List<Task<LaneAnswer<int>>> answers = [AskFrom(testee, 1, call, "first"), AskFrom(testee, 2, call, "second"), AskFrom(testee, 3, call, "third")];
		open.SetResult();
		await Task.WhenAll(answers).WaitAsync(Patience);

		await Assert.That(sent.Select(batch => batch.Context)).IsEquivalentTo(["first", "second"]);
	}

	private static Task<LaneAnswer<int>> AskAs(Lane<int, int> lane, int key, LaneCall<int, int> call, LaneClass asking)
	{
		using var _ = new LanePriority(asking, key).Enter();
		return lane.AskAsync(key, call, CancellationToken.None);
	}

	private static Task<LaneAnswer<int>> AskFrom(Lane<int, int> lane, int key, LaneCall<int, int> call, string asker)
	{
		Asker.Value = asker;
		return lane.AskAsync(key, call, CancellationToken.None);
	}

	private static LaneCall<int, int> Echo(List<SentBatch> sent, Func<int, string> batchKey, Func<LaneClass, Task> wait)
	{
		return new LaneCall<int, int>(
			batchKey,
			async (keys, _) =>
			{
				var asking = LanePriority.Current.Class;
				lock (sent)
				{
					sent.Add(new SentBatch(asking, Asker.Value ?? string.Empty, keys));
				}

				await wait(asking);
				return Success.Of<IReadOnlyDictionary<int, int>>(keys.ToDictionary(key => key, key => key));
			}
		);
	}

	private sealed record SentBatch(LaneClass Class, string Context, IReadOnlyList<int> Keys);
}
