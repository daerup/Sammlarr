using System.Diagnostics;
using Contracts;
using Contracts.Caching;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Providers;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;

namespace Core.Media;

internal sealed class MediaIdResolver(
	IEnumerable<Keyed<RemoteService, IExternalIdConverter>> converters,
	ITypedMemoryCache<string, bool> unmatchedCache,
	IRemoteCapacity capacity,
	IDateTimeProvider clock
)
{
	private static readonly TimeSpan unmatchedTtl = TimeSpan.FromHours(1);

	private readonly IReadOnlyList<Hop> _hops = IdLookupPlanner.Derive(converters);

	public async Task<Result<IReadOnlyList<IExternalMediaId>>> ResolveAsync(
		IdentifyRequest request,
		MediaProvider target,
		CancellationToken cancellationToken = default
	)
	{
		var key = UnmatchedCacheKey.Build(request.Candidates, target);
		return await unmatchedCache
			.GetValueOrNone(key)
			.Match(
				_ => Task.FromResult<Result<IReadOnlyList<IExternalMediaId>>>(Error.From("Candidates were previously resolved as unmatched.")),
				() => LookUpAsync(request, target, key, cancellationToken)
			);
	}

	private async Task<Result<IReadOnlyList<IExternalMediaId>>> LookUpAsync(
		IdentifyRequest request,
		MediaProvider target,
		string key,
		CancellationToken cancellationToken
	)
	{
		var candidates = request.Candidates;
		var plan = IdLookupPlanner.Plan(candidates.Select(candidate => candidate.Source).ToHashSet(), request.Hints.IsSome(), target, _hops, capacity, clock.Now);
		var unresolved = Error.From($"No lookup resolved the candidates to a {target} id.");
		var transient = false;
		var answers = new Dictionary<AnswerKey, LookupAnswer>();
		var down = new HashSet<RemoteService>();
		var waited = new HashSet<RemoteService>();
		foreach (var path in plan.Paths)
		{
			var outcome = await FollowAsync(path, candidates, request.Hints, target, answers, down, waited, cancellationToken);
			if (outcome.Found.IsSome())
			{
				return outcome.Found.Map(found => found.Ids).ToResult(unresolved);
			}
			transient |= outcome.Transient;
		}

		if (!transient)
		{
			unmatchedCache.Set(key, true, unmatchedTtl);
		}
		return unresolved;
	}

	private async Task<StepOutcome> FollowAsync(
		IReadOnlyList<Hop> path,
		IReadOnlyList<IExternalMediaId> candidates,
		Optional<IdentifyHints> hints,
		MediaProvider target,
		Dictionary<AnswerKey, LookupAnswer> answers,
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
		Dictionary<AnswerKey, LookupAnswer> answers,
		HashSet<RemoteService> down,
		CancellationToken cancellationToken
	)
	{
		foreach (var request in Requests(hop, ids, hints))
		{
			var key = AnswerKey.Of(hop.Converter, request);
			if (!answers.TryGetValue(key, out var answer))
			{
				answer = await AskAsync(hop.Converter, request, down, cancellationToken);
				answers[key] = answer;
			}

			if (answer.Transient)
			{
				return new StepOutcome(new None(), true);
			}

			var found = answer.Result.ToOptional().When(result => Holds(hop, result));
			if (found.IsSome())
			{
				return new StepOutcome(found, false);
			}
		}

		return new StepOutcome(new None(), false);
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

	private static async Task<LookupAnswer> AskAsync(
		IExternalIdConverter converter,
		IdLookupRequest request,
		HashSet<RemoteService> down,
		CancellationToken cancellationToken
	)
	{
		try
		{
			var answer = await converter.LookUpAsync(request, cancellationToken);
			return answer.Match(
				_ => new LookupAnswer(answer, false),
				error => error.CausedBy<RemoteUnavailableException>().Match(unavailable => Unavailable(unavailable, down), () => new LookupAnswer(answer, false))
			);
		}
		catch (RemoteUnavailableException unavailable)
		{
			Activity.Current?.AddException(unavailable);
			return Unavailable(unavailable, down);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Activity.Current?.AddException(ex);
			return new LookupAnswer(Error.From(ex), true);
		}
	}

	private static LookupAnswer Unavailable(RemoteUnavailableException unavailable, HashSet<RemoteService> down)
	{
		down.Add(unavailable.Remote);
		return new LookupAnswer(Error.From(unavailable), true);
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
		return blocked.Keys.All(remote => capacity.Of(remote).IsOpen(freeAt));
	}

	private readonly record struct StepOutcome(Optional<IdLookupResult> Found, bool Transient);

	private readonly record struct LookupAnswer(Result<IdLookupResult> Result, bool Transient);

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
