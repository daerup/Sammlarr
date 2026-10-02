using System.Diagnostics;
using Contracts;
using Contracts.Caching;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Providers;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;

namespace Core.Media;

internal sealed class MediaIdResolver(
	IEnumerable<IExternalIdConverter> converters,
	ITypedMemoryCache<string, bool> unmatchedCache,
	IProviderCapacity capacity,
	IDateTimeProvider clock
)
{
	private static readonly TimeSpan unmatchedTtl = TimeSpan.FromHours(1);

	private readonly IReadOnlyList<Hop> _hops = IdLookupPlanner.Derive(converters);

	public async Task<Result<IReadOnlyList<IExternalMediaId>>> ResolveAsync(
		ResolveRequest request,
		MediaProvider target,
		CancellationToken cancellationToken = default
	)
	{
		var candidates = request.Candidates;
		var key = MediaUnmatchedCacheKey.Build(candidates, target);
		if (unmatchedCache.TryGetValue(key, out _))
		{
			return Error.From("Candidates were previously resolved as unmatched.");
		}

		var plan = IdLookupPlanner.Plan(candidates.Select(candidate => candidate.Source).ToHashSet(), request.Hints.IsSome(), target, _hops, capacity, clock.Now);
		var unresolved = Error.From($"No lookup resolved the candidates to a {target} id.");
		var transient = plan.Blocked;
		var answers = new Dictionary<AnswerKey, LookupAnswer>();
		var down = new HashSet<MediaProvider>();
		foreach (var path in plan.Paths)
		{
			var outcome = await FollowAsync(path, candidates, request.Hints, target, answers, down, cancellationToken);
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
		Optional<ResolutionHints> hints,
		MediaProvider target,
		Dictionary<AnswerKey, LookupAnswer> answers,
		HashSet<MediaProvider> down,
		CancellationToken cancellationToken
	)
	{
		var ids = candidates;
		var current = hints;
		for (var index = 0; index < path.Count; index++)
		{
			if (!path.Skip(index).All(remaining => IsOpen(remaining, down)))
			{
				return new StepOutcome(new None(), true);
			}

			var outcome = await TakeAsync(path[index], ids, current, answers, down, cancellationToken);
			if (outcome.Found.IsNone())
			{
				return outcome;
			}

			outcome.Found.Apply(found =>
			{
				ids = [.. ids.Concat(found.Ids).Distinct()];
				current = found.Hints.Or(current);
			});
		}

		return new StepOutcome(ids.Any(id => id.Source == target) ? Some.Of(new IdLookupResult(ids, current)) : new None(), false);
	}

	private static async Task<StepOutcome> TakeAsync(
		Hop hop,
		IReadOnlyList<IExternalMediaId> ids,
		Optional<ResolutionHints> hints,
		Dictionary<AnswerKey, LookupAnswer> answers,
		HashSet<MediaProvider> down,
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

	private static IEnumerable<IdLookupRequest> Requests(Hop hop, IReadOnlyList<IExternalMediaId> ids, Optional<ResolutionHints> hints)
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
		HashSet<MediaProvider> down,
		CancellationToken cancellationToken
	)
	{
		try
		{
			var answer = await converter.LookUpAsync(request, cancellationToken);
			return answer.Match(
				_ => new LookupAnswer(answer, false),
				error => error.Exception is ProviderUnavailableException unavailable ? Unavailable(unavailable, down) : new LookupAnswer(answer, false)
			);
		}
		catch (ProviderUnavailableException unavailable)
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

	private static LookupAnswer Unavailable(ProviderUnavailableException unavailable, HashSet<MediaProvider> down)
	{
		down.Add(unavailable.Provider);
		return new LookupAnswer(Error.From(unavailable), true);
	}

	private bool IsOpen(Hop hop, HashSet<MediaProvider> down)
	{
		var provider = hop.Converter.Provider;
		return !down.Contains(provider) && capacity.Of(provider).IsOpen(clock.Now);
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

		private static string HintsKey(ResolutionHints hints, IReadOnlyList<IExternalMediaId> known)
		{
			var ids = known.Select(id => $"{id}").Order(StringComparer.Ordinal);
			return $"{hints.MediaType}:{hints.Year}:{hints.Title}|{string.Join(",", ids)}";
		}
	}
}
