using System.Collections.Concurrent;
using System.Diagnostics;
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

		var results = await testee.LoadListAsync(Entries(new IdentifyRequest([heat], new None()))).ToListAsync();

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

		var results = await testee.LoadListAsync(Entries(new IdentifyRequest([tvdb], new None()))).ToListAsync();

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
			await foreach (var result in testee.LoadListAsync(Entries([.. Imdb(3)])))
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
			await foreach (var result in testee.LoadListAsync(Entries([.. Imdb(3)])))
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

		await testee.LoadListAsync(Entries([.. Imdb(3)])).ToListAsync();

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

	private static IEnumerable<IdentifyRequest> Imdb(int count)
	{
		return Enumerable.Range(0, count).Select(index => new IdentifyRequest([ImdbId.From($"tt{index}", MediaType.Movie)], new None()));
	}

	private static IAsyncEnumerable<ListTitle> Entries(params IdentifyRequest[] requests)
	{
		return requests.Select(ListTitle (request) => request).ToAsyncEnumerable();
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
}
