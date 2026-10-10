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
		var plan = IdLookupPlanner.Plan(free.Ids.Select(candidate => candidate.Source).ToHashSet(), request.Hints.IsSome(), target, hops, capacity, clock.Now);
		var transient = free.Transient;
		var waited = new HashSet<RemoteService>();
		foreach (var path in plan.Paths)
		{
			using var traced = FeedRunSpans.Path();
			var outcome = await FollowAsync(path, free.Ids, request.Hints, target, answers, down, waited, cancellationToken);
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
		HashSet<RemoteService> waited,
		CancellationToken cancellationToken
	)
	{
		var ids = candidates;
		var current = hints;
		for (var index = 0; index < path.Count; index++)
		{
			if (!await WaitForAsync(path.Skip(index), waited, down, cancellationToken))
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
				told.Key != key
				&& told.Key.Converter == key.Converter
				&& told.Key.Lookup.Equals(key.Lookup)
				&& told.Value.ToOptional().Match(found => found.Ids.Contains(byId.Id), () => false)
			),
			ByHintsRequest => false,
		};
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

	private async Task<bool> WaitForAsync(
		IEnumerable<Hop> remaining,
		HashSet<RemoteService> waited,
		HashSet<RemoteService> down,
		CancellationToken cancellationToken
	)
	{
		var now = clock.Now;
		var blocked = new Dictionary<RemoteService, DateTimeOffset>();
		foreach (var remote in remaining.Select(hop => hop.Remote).Distinct())
		{
			if (down.Contains(remote))
			{
				return false;
			}

			var load = capacity.Of(remote);
			if (load.IsOpen(now))
			{
				continue;
			}

			if (waited.Contains(remote) || !load.FreesWithin(now, load.MaxWait))
			{
				FeedRunSpans.GaveUp(remote, TraceStage.Ids);
				return false;
			}

			blocked[remote] = load.FreeAt(now);
		}

		if (blocked.Count == 0)
		{
			return true;
		}

		waited.UnionWith(blocked.Keys);
		var freeAt = blocked.Values.Max();
		await clock.WaitUntilAsync(freeAt, cancellationToken);
		var stillBlocked = blocked.Keys.Where(remote => !capacity.Of(remote).IsOpen(freeAt)).ToList();
		foreach (var remote in stillBlocked)
		{
			FeedRunSpans.GaveUp(remote, TraceStage.Ids);
		}

		return stillBlocked.Count == 0;
	}

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
