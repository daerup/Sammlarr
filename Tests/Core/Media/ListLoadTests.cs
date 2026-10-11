using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Feed;
using Domain.Media;
using Domain.Providers;
using Observability.Feed;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Core.Tests.Media;

public class ListLoadTests
{
	[Test]
	public async Task LoadListAsync_ForAPagedSource_ReadsTheLibraryOncePerPage()
	{
		var (testee, deps) = TestMediaService.Create();

		await testee.LoadListAsync(Paged([3, 2])).ToListAsync();

		deps.MediaRepo.GetByExternalsAsync(Any(), Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task LoadListAsync_ForAFreshHit_SettlesKnownWithoutAnyCall()
	{
		var heat = TmdbId.From("949", MediaType.Movie);
		var (testee, deps) = TestMediaService.Create();
		deps.MediaRepo.GetByExternalsAsync(Any(), Any()).Returns([Stored(MediaId.New(), heat)]);

		var results = await testee.LoadListAsync(ListTitles.Of(new IdentifyRequest([heat], new None()))).ToListAsync();

		await Assert
			.That(results.Single().Item)
			.IsOfUnionType<MediaMatchResult, MatchedMedia>()
			.And.Multiple(async matched => await Assert.That(matched.Known).IsTrue());
		deps.Converter.LookUpAsync(Any(), Any()).WasNeverCalled();
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any()).WasNeverCalled();
		deps.Claims.ClaimAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task LoadListAsync_ForARowWithoutTheSinkId_ResolvesFromTheStoredIds()
	{
		var tvdb = TvdbId.From("81189", MediaType.Movie);
		var imdb = ImdbId.From("tt0137523", MediaType.Movie);
		var (testee, deps) = TestMediaService.Create();
		deps.MediaRepo.GetByExternalsAsync(Any(), Any()).Returns([Stored(MediaId.New(), tvdb, imdb)]);
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns((external, _) => new Found<FoundMedia>(TestMediaService.CompleteMovie(external)));

		var results = await testee.LoadListAsync(ListTitles.Of(new IdentifyRequest([tvdb], new None()))).ToListAsync();

		await Assert.That(results.Single().Item).IsOfUnionType<MediaMatchResult, MatchedMedia>();
		deps.Converter.LookUpAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task LoadListAsync_WhenTheLibraryReadFails_SettlesItsBurstAndFails()
	{
		var (testee, deps) = TestMediaService.Create();
		deps.MediaRepo.GetByExternalsAsync(Any(), Any()).Throws(new InvalidOperationException("the library is gone"));
		var settled = new List<Listed<MediaMatchResult>>();

		var act = async () =>
		{
			await foreach (var result in testee.LoadListAsync(ListTitles.Of(Imdb(3))))
			{
				settled.Add(result);
			}
		};

		await Assert.That(act).Throws<InvalidOperationException>();
		await Assert.That(settled.Select(result => result.Position)).IsEquivalentTo([0, 1, 2]);
		await Assert.That(settled.Select(result => Reason(result.Item))).IsEquivalentTo([.. Enumerable.Repeat(UnmatchedReason.CouldNotLookUp, 3)]);
	}

	[Test]
	public async Task LoadListAsync_WhenAChainThrows_SettlesItAsCouldNotLookUpAndFailsTheLoad()
	{
		var (testee, deps) = TestMediaService.Create();
		deps.Converter.LookUpAsync(Any(), Any())
			.Returns(
				(request, _) =>
					request is ByIdRequest(_, var id) && id.Value == "tt1"
						? throw new InvalidOperationException("the lookup broke")
						: TestMediaService.FoundByFind(request)
			);
		var settled = new List<Listed<MediaMatchResult>>();

		var act = async () =>
		{
			await foreach (var result in testee.LoadListAsync(ListTitles.Of(Imdb(3))))
			{
				settled.Add(result);
			}
		};

		await Assert.That(act).Throws<InvalidOperationException>().WithMessageContaining("the lookup broke");
		await Assert.That(settled).Count().IsEqualTo(3);
		await Assert.That(Reason(settled.Single(result => result.Position == 1).Item)).IsEqualTo(UnmatchedReason.CouldNotLookUp);
	}

	[Test]
	public async Task LoadListAsync_SettlesEveryEntryOnce_IncludingEntriesWithoutIds()
	{
		var (testee, _) = TestMediaService.Create();
		var idless = Idless();
		ListTitle[] entries = [.. Imdb(3).Select(ListTitle (request) => request), idless, idless];

		var results = await testee.LoadListAsync(entries.ToAsyncEnumerable()).ToListAsync();

		await Assert.That(results.Select(result => result.Position)).IsEquivalentTo([0, 1, 2, 3, 4]);
		await Assert.That(results.Where(result => result.Position >= 3).Select(result => result.Item)).IsEquivalentTo([Settled(idless), Settled(idless)]);
	}

	[Test]
	public async Task LoadListAsync_WhileATitleStalls_HandsOutAnEntryWithoutIdsRightAway()
	{
		var (testee, deps) = TestMediaService.Create();
		var stalled = new TaskCompletionSource<Answer<IdLookupResult>>();
		deps.Converter.LookUpAsync(Any(), Any()).ReturnsAsync(stalled.Task);
		var idless = Idless();
		ListTitle[] entries = [.. Imdb(1).Select(ListTitle (request) => request), idless];
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		await using var results = testee.LoadListAsync(entries.ToAsyncEnumerable(), timeout.Token).GetAsyncEnumerator(timeout.Token);

		await results.MoveNextAsync();
		stalled.SetResult(new NoMatch(RemoteService.Tmdb));

		await Assert.That(results.Current).IsEqualTo(new Listed<MediaMatchResult>(1, Settled(idless)));
	}

	[Test]
	public async Task LoadListAsync_StartsTitleSpansInListOrder_WhenTitlesSettleOutOfOrder()
	{
		var started = new ConcurrentQueue<Activity>();
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == FeedRunSpans.SourceName,
			Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
			ActivityStarted = started.Enqueue,
		};
		ActivitySource.AddActivityListener(listener);
		var (testee, deps) = TestMediaService.Create();
		deps.Converter.LookUpAsync(Any(), Any())
			.ReturnsAsync(
				async (request, token) =>
				{
					await Task.Delay(TimeSpan.FromMilliseconds(request is ByIdRequest(_, var id) && id.Value == "tt0" ? 50 : 1), token);
					return TestMediaService.FoundByFind(request);
				}
			);
		var run = FeedRunSpans.Run(FeedId.From(Guid.NewGuid()), Guid.NewGuid());
		using var entered = FeedRunSpans.Enter(run);

		await testee.LoadListAsync(ListTitles.Of(Imdb(3))).ToListAsync();

		var traceId = run.Match(span => span.TraceId, () => default);
		var titles = started
			.Where(span => span.TraceId == traceId && span.OperationName == FeedRunNames.Title)
			.Select(span => $"{span.GetTagItem(FeedRunTags.Name)}");
		await Assert.That(titles).IsEquivalentTo([.. Imdb(3).Select(request => string.Join(", ", request.Candidates))], CollectionOrdering.Matching);
	}

	private static UnmatchedMedia Idless() => new([], new MovieDetails { Title = "No Ids" });

	private static MediaMatchResult Settled(UnmatchedMedia unmatched) => unmatched;

	private static UnmatchedReason Reason(MediaMatchResult result)
	{
		return result switch
		{
			UnmatchedMedia unmatched => unmatched.Reason,
			MatchedMedia => throw new InvalidOperationException("The title matched."),
		};
	}

	private static StoredMedia Stored(MediaId id, params IIdentityKey[] keys)
	{
		return new StoredMedia(
			new MediaIdentity(id, [.. keys]),
			Some.Of(new CachedDetails(new MovieDetails { Title = "Heat" }, TestMediaService.Now, TestMediaService.Now, []))
		);
	}

	private static List<IdentifyRequest> Imdb(int count)
	{
		return [.. Enumerable.Range(0, count).Select(index => new IdentifyRequest([ImdbId.From(Tt(index), MediaType.Movie)], new None()))];
	}

	private static async IAsyncEnumerable<ListTitle> Paged(int[] pages)
	{
		var next = 0;
		foreach (var size in pages)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(20));
			foreach (var _ in Enumerable.Range(0, size))
			{
				yield return new IdentifyRequest([ImdbId.From($"tt{next++}", MediaType.Movie)], new None());
			}
		}
	}

	[Test]
	public async Task LoadListAsync_WhenLookupsFinishOutOfOrder_HandsEachOutAsItSettlesAtItsListPosition()
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

		var results = await testee.LoadListAsync(ListTitles.Of(Imdb(30))).ToListAsync();

		await Assert.That(results[0].Position).IsNotEqualTo(0);
		await Assert
			.That(results.OrderBy(result => result.Position).Select(result => result.Item.Externals.OfType<ImdbId>().Single().Value))
			.IsEquivalentTo([.. Enumerable.Range(0, 30).Select(Tt)], CollectionOrdering.Matching);
	}

	[Test]
	public async Task LoadListAsync_WhenConsumerStopsEarly_StopsReadingTheList()
	{
		var (testee, _) = TestMediaService.Create();
		var pulled = 0;
		var closed = new TaskCompletionSource();

		await foreach (var _ in testee.LoadListAsync(Counting(4000, () => Interlocked.Increment(ref pulled), closed).AsListTitles()).InListOrderAsync())
		{
			break;
		}

		await Assert.That(closed.Task.IsCompleted).IsTrue();
		await Assert.That(Volatile.Read(ref pulled)).IsGreaterThanOrEqualTo(1);
	}

	[Test]
	public async Task LoadListAsync_WhenTheListFailsAfterSomeTitles_YieldsThemThenThrows()
	{
		var (testee, _) = TestMediaService.Create();
		var seen = new List<MediaMatchResult>();

		var act = async () =>
		{
			await foreach (var result in testee.LoadListAsync(FailsAfter(25).AsListTitles()).InListOrderAsync())
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
	public async Task LoadListAsync_ForTwoEntriesOfOneMedia_GivesBothTheSameId()
	{
		var (testee, deps) = TestMediaService.Create();
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns((ext, _) => new Found<FoundMedia>(TestMediaService.CompleteMovie(ext)));
		var requests = Imdb(20);
		requests[15] = requests[2];

		var results = (await testee.LoadListAsync(requests.ToAsyncEnumerable().AsListTitles()).InListOrderAsync().ToListAsync()).Matched().ToList();

		await Assert.That(results).Count().IsEqualTo(20);
		await Assert.That(results[15].Identity.Id).IsEqualTo(results[2].Identity.Id);
	}

	[Test]
	public async Task LoadListAsync_WhenCancelled_LeavesNoLookupRunning()
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

		var consume = testee
			.LoadListAsync(Imdb(50).ToAsyncEnumerable().AsListTitles(), cancellation.Token)
			.InListOrderAsync(cancellation.Token)
			.ToListAsync()
			.AsTask();
		await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
		await cancellation.CancelAsync();

		await Assert.That(async () => await consume.WaitAsync(TimeSpan.FromSeconds(5))).Throws<OperationCanceledException>();
		await Assert.That(Volatile.Read(ref running)).IsEqualTo(0);
	}

	[Test]
	public async Task LoadListAsync_WhenATitlesIdsBelongToTwoStoredTitles_LeavesItUnmatchedAndGoesOn()
	{
		var conflicting = ImdbId.From(Tt(1), MediaType.Movie);
		var (testee, deps) = TestMediaService.Create();
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns((ext, _) => new Found<FoundMedia>(TestMediaService.CompleteMovie(ext)));
		var owners = new[] { MediaId.New(), MediaId.New() };
		deps.Claims.ClaimAsync(Any(), Any()).Returns((claim, _) => Outcome(claim, conflicting, owners));

		var results = await testee.LoadListAsync(Imdb(3).ToAsyncEnumerable().AsListTitles()).InListOrderAsync().ToListAsync();

		await Assert.That(results[0]).IsOfUnionType<MediaMatchResult, MatchedMedia>();
		var unmatched = await Assert.That(results[1]).IsOfUnionType<MediaMatchResult, UnmatchedMedia>();
		await Assert.That(unmatched!.Reason).IsEqualTo(UnmatchedReason.Conflict);
		await Assert.That(unmatched.ConflictsWith).IsEquivalentTo(owners);
		await Assert.That(results[2]).IsOfUnionType<MediaMatchResult, MatchedMedia>();
	}

	private static ClaimOutcome Outcome(IdentityClaim claim, IIdentityKey conflicting, MediaId[] owners)
	{
		return claim.Keys.Contains(conflicting) ? new Conflicted([.. owners], [conflicting]) : new Claimed(MediaId.New(), claim.Keys);
	}

	[Test]
	public async Task LoadListAsync_WhenALaterEntryBridgesTwoTitles_SettlesItAsAConflict()
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
					if (Interlocked.Increment(ref detailCalls) == 2)
					{
						all.TrySetResult();
					}

					await all.Task.WaitAsync(token);
					return new Found<FoundMedia>(TestMediaService.CompleteMovie(ext));
				}
			);
		deps.Claims.ClaimAsync(Any(), Any()).Returns((claim, _) => Bridging(claim, first, second));
		List<IdentifyRequest> requests = [new([first], new None()), new([second], new None()), new([first, second], new None())];

		var results = await testee.LoadListAsync(requests.ToAsyncEnumerable().AsListTitles(), timeout.Token).InListOrderAsync(timeout.Token).ToListAsync();

		await Assert.That(results.Matched()).Count().IsEqualTo(2);
		var unmatched = await Assert.That(results[2]).IsOfUnionType<MediaMatchResult, UnmatchedMedia>();
		await Assert.That(unmatched!.Reason).IsEqualTo(UnmatchedReason.Conflict);
	}

	private static ClaimOutcome Bridging(IdentityClaim claim, IIdentityKey first, IIdentityKey second)
	{
		return claim.Keys.Contains(first) && claim.Keys.Contains(second)
			? new Conflicted([MediaId.New(), MediaId.New()], claim.Keys)
			: new Claimed(MediaId.New(), claim.Keys);
	}

	[Test]
	public async Task LoadListAsync_WhenOneTitleStalls_SettlesTheOthers()
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

					return new Found<FoundMedia>(TestMediaService.CompleteMovie(ext));
				}
			);

		var consume = testee.LoadListAsync(Imdb(150).ToAsyncEnumerable().AsListTitles(), timeout.Token).InListOrderAsync(timeout.Token).ToListAsync().AsTask();
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
