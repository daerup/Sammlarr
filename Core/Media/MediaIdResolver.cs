using System.Diagnostics;
using Contracts;
using Contracts.Caching;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Providers;
using Domain.Feed.Trace;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;
using Observability.Feed;

namespace Core.Media;

internal sealed class MediaIdResolver(
	IEnumerable<Keyed<RemoteService, IExternalIdConverter>> converters,
	ITypedMemoryCache<string, bool> unmatchedCache,
	IRemoteCapacity capacity,
	IServiceReadiness readiness,
	IDateTimeProvider clock
)
{
	private static readonly TimeSpan unmatchedTtl = TimeSpan.FromHours(1);

	private readonly IReadOnlyList<Hop> _hops = IdLookupPlanner.Derive(converters);

	public Task<IReadOnlySet<RemoteService>> ConnectedAsync(CancellationToken cancellationToken)
	{
		return readiness.ConnectedAsync(_hops.Select(hop => hop.Remote), cancellationToken);
	}

	public async Task<Resolution> ResolveAsync(
		IdentifyRequest request,
		MediaProvider target,
		IReadOnlySet<RemoteService> connected,
		CancellationToken cancellationToken = default
	)
	{
		var key = UnmatchedCacheKey.Build(request.Candidates, target, connected);
		return await unmatchedCache
			.GetValueOrNone(key)
			.Apply(_ => FeedRunSpans.NotFoundEarlier())
			.Match(_ => Task.FromResult<Resolution>(new NotFoundEarlier()), () => LookUpAsync(request, target, connected, key, cancellationToken));
	}

	private async Task<Resolution> LookUpAsync(
		IdentifyRequest request,
		MediaProvider target,
		IReadOnlySet<RemoteService> connected,
		string key,
		CancellationToken cancellationToken
	)
	{
		IReadOnlyList<Hop> hops = [.. _hops.Where(hop => connected.Contains(hop.Remote))];
		var answers = new Dictionary<AnswerKey, Answer<IdLookupResult>>();
		var down = new HashSet<RemoteService>();
		var free = await FreeAsync(request.Candidates, target, hops, answers, down, cancellationToken);
		var plan = IdLookupPlanner.Plan(free.Ids.Select(candidate => candidate.Source).ToHashSet(), request.Hints.IsSome(), target, hops, capacity);
		var transient = free.Transient;
		foreach (var path in plan.Paths.Where(path => !AnsweredOnce(path, answers)))
		{
			using var traced = FeedRunSpans.Path();
			var outcome = await FollowAsync(path, free.Ids, request.Hints, target, answers, down, cancellationToken);
			if (outcome.Found is not Some<IdLookupResult>(var found))
			{
				transient |= outcome.Transient;
				continue;
			}

			return new Resolved(found.Ids, [.. free.Sources, .. path.Select(hop => hop.Remote)]);
		}

		if (transient)
		{
			return new NotRun();
		}

		unmatchedCache.Set(key, true, unmatchedTtl);
		return new NoMatchAnywhere();
	}

	private static bool AnsweredOnce(IReadOnlyList<Hop> path, Dictionary<AnswerKey, Answer<IdLookupResult>> answers)
	{
		return path.Any(hop =>
			hop is IdToId { Lookup.AsksOnce: true } once
			&& answers.Any(told => told.Key.Converter == once.Converter && told.Key.Lookup.Equals((IdLookup)once.Lookup) && Missed(told.Value, once.Lookup))
		);
	}

	private static async Task<(IReadOnlyList<IExternalMediaId> Ids, bool Transient, IReadOnlySet<RemoteService> Sources)> FreeAsync(
		IReadOnlyList<IExternalMediaId> candidates,
		MediaProvider target,
		IReadOnlyList<Hop> hops,
		Dictionary<AnswerKey, Answer<IdLookupResult>> answers,
		HashSet<RemoteService> down,
		CancellationToken cancellationToken
	)
	{
		var ids = candidates;
		var transient = false;
		var sources = new HashSet<RemoteService>();
		foreach (var hop in hops.Where(hop => hop.Cost == 0))
		{
			foreach (
				var request in hop switch
				{
					IdToId free when ids.All(id => id.Source != free.To) => ByIdRequests(free.Lookup, free.From, candidates),
					IdToId or IdToHints or HintsToId => [],
				}
			)
			{
				var key = AnswerKey.Of(hop.Converter, request);
				if (!answers.TryGetValue(key, out var answer))
				{
					answer = await AskAsync(hop.Converter, request, down, cancellationToken);
					answers[key] = answer;
				}

				transient |= !answer.IsDefinitive && hop is IdToId { To: var to } && to == target;
				var held = ids.Count;
				answer.ToOptional().Apply(found => ids = [.. ids.Concat(found.Ids.Where(id => ids.All(each => each.Source != id.Source))).Distinct()]);
				if (ids.Count > held)
				{
					FeedRunSpans.FreeHop(hop.Remote);
					sources.Add(hop.Remote);
				}
			}
		}

		return (ids, transient, sources);
	}

	private async Task<StepOutcome> FollowAsync(
		IReadOnlyList<Hop> path,
		IReadOnlyList<IExternalMediaId> candidates,
		Optional<IdentifyHints> hints,
		MediaProvider target,
		Dictionary<AnswerKey, Answer<IdLookupResult>> answers,
		HashSet<RemoteService> down,
		CancellationToken cancellationToken
	)
	{
		var ids = candidates;
		var current = hints;
		for (var index = 0; index < path.Count; index++)
		{
			if (!Takes(path.Skip(index), down))
			{
				return new StepOutcome(new None(), true);
			}

			var outcome = await TakeAsync(path[index], ids, current, answers, down, cancellationToken);
			if (outcome.Found.IsNone())
			{
				return outcome;
			}

			outcome.Found.Apply(found => ids = [.. ids.Concat(found.Ids).Distinct()]).Apply(found => current = found.Hints.Or(current));
		}

		return new StepOutcome(Optional.When(ids.Any(id => id.Source == target), () => new IdLookupResult(ids, current)), false);
	}

	private static async Task<StepOutcome> TakeAsync(
		Hop hop,
		IReadOnlyList<IExternalMediaId> ids,
		Optional<IdentifyHints> hints,
		Dictionary<AnswerKey, Answer<IdLookupResult>> answers,
		HashSet<RemoteService> down,
		CancellationToken cancellationToken
	)
	{
		foreach (var request in Requests(hop, ids, hints))
		{
			var key = AnswerKey.Of(hop.Converter, request);
			if (Told(answers, key, request))
			{
				continue;
			}

			if (!answers.TryGetValue(key, out var answer))
			{
				using (var span = FeedRunSpans.Hop(hop.Remote, TraceStage.Ids))
				{
					answer = await AskAsync(hop.Converter, request, down, cancellationToken);
					span.Answered(answer.IsDefinitive ? HopOutcome.Answered : HopOutcome.Failed);
				}

				answers[key] = answer;
			}

			if (!answer.IsDefinitive)
			{
				return new StepOutcome(new None(), true);
			}

			var found = answer.ToOptional().When(result => Holds(hop, result));
			if (found.IsSome())
			{
				return new StepOutcome(found, false);
			}
		}

		return new StepOutcome(new None(), false);
	}

	private static bool Told(Dictionary<AnswerKey, Answer<IdLookupResult>> answers, AnswerKey key, IdLookupRequest request)
	{
		return request switch
		{
			ByIdRequest byId => answers.Any(told =>
				told.Key != key && told.Key.Converter == key.Converter && told.Key.Lookup.Equals(key.Lookup) && Covers(told.Value, byId)
			),
			ByHintsRequest => false,
		};
	}

	private static bool Covers(Answer<IdLookupResult> answer, ByIdRequest request)
	{
		return (request.Lookup.AsksOnce && answer.IsDefinitive) || answer.ToOptional().Match(found => found.Ids.Contains(request.Id), () => false);
	}

	private static bool Missed(Answer<IdLookupResult> answer, ById lookup)
	{
		return answer.IsDefinitive && !answer.ToOptional().Match(found => found.Ids.Any(id => lookup.Yields.Contains(id.Source)), () => false);
	}

	private static IEnumerable<IdLookupRequest> Requests(Hop hop, IReadOnlyList<IExternalMediaId> ids, Optional<IdentifyHints> hints)
	{
		return hop switch
		{
			IdToId id => ByIdRequests(id.Lookup, id.From, ids),
			IdToHints toHints => ByIdRequests(toHints.Lookup, toHints.From, ids),
			HintsToId search => hints.Match(given => new IdLookupRequest[] { new ByHintsRequest(search.Lookup, given, ids) }, () => []),
		};
	}

	private static IEnumerable<IdLookupRequest> ByIdRequests(ById lookup, MediaProvider from, IReadOnlyList<IExternalMediaId> ids)
	{
		return ids.Where(id => id.Source == from).Select(id => (IdLookupRequest)new ByIdRequest(lookup, id));
	}

	private static bool Holds(Hop hop, IdLookupResult found)
	{
		return hop switch
		{
			IdToId id => found.Ids.Any(each => each.Source == id.To),
			HintsToId search => found.Ids.Any(each => each.Source == search.To),
			IdToHints => found.Hints.IsSome(),
		};
	}

	private static async Task<Answer<IdLookupResult>> AskAsync(
		IExternalIdConverter converter,
		IdLookupRequest request,
		HashSet<RemoteService> down,
		CancellationToken cancellationToken
	)
	{
		var answer = await converter.LookUpAsync(request, cancellationToken);
		switch (answer)
		{
			case Unavailable unavailable:
				down.Add(unavailable.Remote);
				break;
			case Faulted faulted:
				faulted.Error.Exception.Apply(exception => Activity.Current?.AddException(exception));
				break;
		}

		return answer;
	}

	private bool Takes(IEnumerable<Hop> remaining, HashSet<RemoteService> down)
	{
		var now = clock.Now;
		var refused = remaining.Select(hop => hop.Remote).Distinct().FirstOrNone(remote => down.Contains(remote) || !WithinMaxWait(capacity.Of(remote), now));
		refused.When(remote => !down.Contains(remote)).Apply(remote => FeedRunSpans.GaveUp(remote, TraceStage.Ids));
		return refused.IsNone();
	}

	private static bool WithinMaxWait(RemoteLoad load, DateTimeOffset now) => load.Takes(now, load.MaxWait);

	private readonly record struct StepOutcome(Optional<IdLookupResult> Found, bool Transient);

	private readonly record struct AnswerKey(IExternalIdConverter Converter, IdLookup Lookup, string Request)
	{
		public static AnswerKey Of(IExternalIdConverter converter, IdLookupRequest request)
		{
			return request switch
			{
				ByIdRequest byId => new AnswerKey(converter, byId.Lookup, $"{byId.Id}"),
				ByHintsRequest search => new AnswerKey(converter, search.Lookup, HintsKey(search.Hints, search.Known)),
			};
		}

		private static string HintsKey(IdentifyHints hints, IReadOnlyList<IExternalMediaId> known)
		{
			var ids = known.Select(id => $"{id}").Order(StringComparer.Ordinal);
			return $"{hints.MediaType}:{hints.Year}:{hints.Title}|{string.Join(",", ids)}";
		}
	}
}
