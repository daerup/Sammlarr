using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Contracts.Media;
using Contracts.Media.Conversions;
using Core.Wantlist.Sync;
using Domain.Feed.Trace;
using Domain.Media;
using Domain.Providers;
using Observability.Feed;
using Shared.Concurrency;
using Shared.Options;

namespace Core.Media;

internal sealed class ListLoad(
	TitleIdentifier identifier,
	MediaIdResolver idResolver,
	DetailsFetcher detailsFetcher,
	LibraryWriterFactory writersFactory,
	ActiveSink activeSink,
	IMediaRepository mediaRepository,
	BurstPrimers primers,
	FeedLoadOptions options,
	TimeProvider time
)
{
	public IAsyncEnumerable<Listed<MediaMatchResult>> LoadAsync(IAsyncEnumerable<ListTitle> entries, FieldNeeds needs, CancellationToken cancellationToken)
	{
		return Channel.CreateUnbounded<Listed<MediaMatchResult>>().StreamAsync((settled, token) => RunAsync(entries, needs, settled, token), cancellationToken);
	}

	private async Task RunAsync(
		IAsyncEnumerable<ListTitle> entries,
		FieldNeeds needs,
		ChannelWriter<Listed<MediaMatchResult>> settled,
		CancellationToken cancellationToken
	)
	{
		await using var writer = writersFactory.Create();
		var context = await LoadContext.StartAsync(activeSink, idResolver, detailsFetcher, needs, writer, cancellationToken);
		await using var watchdog = new RunWatchdog(time, options.StallAfter, cancellationToken);
		using var primes = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		var primings = new ConcurrentBag<Task>();
		var titles = new List<Task>();
		try
		{
			await foreach (var burst in FeedRunSpans.SourceAsync(entries, entry => entry is IdentifyRequest, cancellationToken).BurstsAsync(cancellationToken))
			{
				var listed = burst.Select(Traced).Numbered(titles.Count);
				var shelf = ShelveAndPrimeAsync(listed, context, primings, (primes.Token, cancellationToken));
				titles.AddRange(listed.Select(title => SettleAsync(title, shelf, context, settled, watchdog)));
			}
		}
		finally
		{
			await Task.WhenAll(titles).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
			await primes.CancelAsync();
			await Task.WhenAll(primings).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
		}

		await Task.WhenAll(titles);
	}

	private async Task<Shelf> ShelveAndPrimeAsync(
		IReadOnlyList<Listed<TitleWork>> listed,
		LoadContext context,
		ConcurrentBag<Task> primings,
		(CancellationToken Primes, CancellationToken Load) tokens
	)
	{
		var shelf = await ShelveAsync(listed, tokens.Load);
		using var priority = LanePriority.Next(LaneClass.Title).Enter();
		List<PrimedTitle> titles =
		[
			.. listed
				.Select(title => title.Item.Request)
				.WhereSome()
				.Where(request => context.AddressedBy(request.Type).IsSome())
				.Select(request => Primed(request, shelf.Rows(request))),
		];
		primings.Add(primers.PrimeAsync(titles, context.Details, tokens.Primes));
		return shelf;
	}

	private static PrimedTitle Primed(IdentifyRequest request, IReadOnlyList<StoredMedia> rows)
	{
		IReadOnlyList<IExternalMediaId> ids = [.. request.Candidates.Concat(rows.SelectMany(row => row.Identity.Keys)).Distinct()];
		return new PrimedTitle(ids, rows.FirstOrNone().Map(row => row.Cached));
	}

	private async Task<Shelf> ShelveAsync(IReadOnlyList<Listed<TitleWork>> listed, CancellationToken cancellationToken)
	{
		IReadOnlyList<IExternalMediaId> keys = [.. listed.SelectMany(title => title.Item.Candidates).Distinct()];
		if (keys.Count == 0)
		{
			return new Shelf(new Dictionary<IExternalMediaId, StoredMedia>());
		}

		var rows = await mediaRepository.GetByExternalsAsync(keys, cancellationToken);
		return new Shelf(
			rows.SelectMany(row => row.Identity.Keys.Select(key => (Key: (IExternalMediaId)key, Row: row))).ToDictionary(pair => pair.Key, pair => pair.Row)
		);
	}

	private async Task SettleAsync(
		Listed<TitleWork> title,
		Task<Shelf> shelf,
		LoadContext context,
		ChannelWriter<Listed<MediaMatchResult>> settled,
		RunWatchdog watchdog
	)
	{
		using var priority = LanePriority.Next(LaneClass.Title).Enter();
		watchdog.Started();
		Optional<MediaMatchResult> outcome = new None();
		try
		{
			outcome = Some.Of(await ChainAsync(title.Item, await shelf.WaitAsync(watchdog.Token), context, watchdog.Token));
		}
		catch (OperationCanceledException) when (watchdog.Starved)
		{
			outcome = Some.Of(title.Item.CouldNotLookUp());
		}
		finally
		{
			watchdog.Settled();
			var result = outcome.GetValueOr(() => title.Item.CouldNotLookUp());
			FeedRunSpans.Settle(title.Item.Span, Fate(result));
			settled.TryWrite(new Listed<MediaMatchResult>(title.Position, result));
		}
	}

	private async Task<MediaMatchResult> ChainAsync(TitleWork work, Shelf shelf, LoadContext context, CancellationToken cancellationToken)
	{
		using var entered = FeedRunSpans.Enter(work.Span);
		return work.Entry switch
		{
			IdentifyRequest request => await context
				.AddressedBy(request.Type)
				.Match(
					target => TitleAsync(request, target, shelf.Rows(request), context, cancellationToken),
					() => Task.FromResult<MediaMatchResult>(request.Unmatched(UnmatchedReason.NotFound))
				),
			UnmatchedMedia unmatched => unmatched,
		};
	}

	private async Task<MediaMatchResult> TitleAsync(
		IdentifyRequest request,
		MediaProvider target,
		IReadOnlyList<StoredMedia> rows,
		LoadContext context,
		CancellationToken cancellationToken
	)
	{
		var identification = await identifier.IdentifyAsync(request, target, rows, context.IdServices, cancellationToken);
		return identification switch
		{
			IdentifyHandoff handoff => await DetailsAsync(request, handoff, context, cancellationToken),
			Unidentified unidentified => request.Unmatched(unidentified.Reason) with { ConflictsWith = unidentified.ConflictsWith },
		};
	}

	private async Task<MediaMatchResult> DetailsAsync(IdentifyRequest request, IdentifyHandoff handoff, LoadContext context, CancellationToken cancellationToken)
	{
		var loaded = await detailsFetcher.LoadAsync(handoff, request.Described(), context.Details, cancellationToken);
		return loaded.Match<MatchedMedia, MediaMatchResult>(
			matched => matched with { Known = handoff.Stored.IsSome() },
			error => request.Unmatched(error.Exception.Map(_ => UnmatchedReason.CouldNotLookUp).GetValueOr(UnmatchedReason.NotFound))
		);
	}

	private static TitleWork Traced(ListTitle entry)
	{
		return entry switch
		{
			IdentifyRequest request => new TitleWork(entry, TitleSpan(request)),
			UnmatchedMedia => Unlisted(entry),
		};
	}

	private static TitleWork Unlisted(ListTitle entry)
	{
		FeedRunSpans.ListedWithoutIds();
		return new TitleWork(entry, new None());
	}

	private static Optional<Activity> TitleSpan(IdentifyRequest request)
	{
		var hints = request.Hints.Or(request.Description);
		return FeedRunSpans.Title(
			hints.Map(found => found.Title).GetValueOr(() => string.Join(", ", request.Candidates)),
			hints.Map(found => found.Year).When(year => year > 0)
		);
	}

	private static TitleFate Fate(MediaMatchResult result)
	{
		return result switch
		{
			MatchedMedia => TitleFate.Matched,
			UnmatchedMedia unmatched => Fate(unmatched.Reason),
		};
	}

	private static TitleFate Fate(UnmatchedReason reason)
	{
		return reason switch
		{
			UnmatchedReason.NotFound => TitleFate.NotFound,
			UnmatchedReason.NotFoundEarlier => TitleFate.NotFound,
			UnmatchedReason.CouldNotLookUp => TitleFate.CouldNotLookUp,
			UnmatchedReason.Conflict => TitleFate.Conflict,
		};
	}

	private readonly record struct TitleWork(ListTitle Entry, Optional<Activity> Span)
	{
		internal IEnumerable<IExternalMediaId> Candidates
		{
			get
			{
				return Entry switch
				{
					IdentifyRequest request => request.Candidates,
					UnmatchedMedia => [],
				};
			}
		}

		internal Optional<IdentifyRequest> Request
		{
			get
			{
				return Entry switch
				{
					IdentifyRequest request => Some.Of(request),
					UnmatchedMedia => new None(),
				};
			}
		}

		internal MediaMatchResult CouldNotLookUp()
		{
			return Entry switch
			{
				IdentifyRequest request => request.Unmatched(UnmatchedReason.CouldNotLookUp),
				UnmatchedMedia unmatched => unmatched,
			};
		}
	}

	private readonly record struct Shelf(IReadOnlyDictionary<IExternalMediaId, StoredMedia> ByKey)
	{
		internal IReadOnlyList<StoredMedia> Rows(IdentifyRequest request)
		{
			return [.. request.Candidates.Select(ByKey.GetValueOrNone).WhereSome().DistinctBy(row => row.Identity.Id)];
		}
	}
}

file static class IdentifyRequestExtensions
{
	extension(IdentifyRequest request)
	{
		public UnmatchedMedia Unmatched(UnmatchedReason reason)
		{
			return new UnmatchedMedia([.. request.Candidates], Details(request.Hints.Or(() => request.Description), request.Type), reason);
		}

		public MediaDetails Described()
		{
			return Details(request.Hints, request.Type);
		}
	}

	private static MediaDetails Details(Optional<IdentifyHints> hints, MediaType type)
	{
		return hints.Map(Hinted).GetValueOr(() => MediaDetails.Empty(type));
	}

	private static MediaDetails Hinted(IdentifyHints hints)
	{
		return MediaDetails.Empty(hints.MediaType) with { Title = hints.Title, ReleaseDate = hints.Year > 0 ? new DateOnly(hints.Year, 1, 1) : null };
	}
}
