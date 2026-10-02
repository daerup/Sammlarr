using System.Diagnostics;
using Contracts;
using Contracts.Caching;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Providers;
using Domain.Media;
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

	private readonly IReadOnlyList<RouteStep> _steps = [.. converters.SelectMany(converter => converter.Routes.Select(route => new RouteStep(converter, route)))];

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

		var plan = IdRoutePlanner.Plan(
			candidates.Select(candidate => candidate.Source).ToHashSet(),
			request.Hints is Some<ResolutionHints>,
			target,
			_steps,
			capacity.Of,
			clock.Now
		);
		var transient = plan.Blocked;
		var tried = new Dictionary<string, StepOutcome>();
		var down = new HashSet<MediaProvider>();
		foreach (var path in plan.Paths)
		{
			var outcome = await FollowAsync(path, candidates, request.Hints, target, tried, down, cancellationToken);
			if (outcome.Found is { } found)
			{
				return Success.Of(found);
			}
			transient |= outcome.Transient;
		}

		if (!transient)
		{
			unmatchedCache.Set(key, true, unmatchedTtl);
		}
		return Error.From($"No route resolved the candidates to a {target} id.");
	}

	private async Task<StepOutcome> FollowAsync(
		IReadOnlyList<RouteStep> path,
		IReadOnlyList<IExternalMediaId> candidates,
		Optional<ResolutionHints> hints,
		MediaProvider target,
		Dictionary<string, StepOutcome> tried,
		HashSet<MediaProvider> down,
		CancellationToken cancellationToken
	)
	{
		IReadOnlyList<IExternalMediaId> current = candidates;
		for (var hop = 0; hop < path.Count; hop++)
		{
			if (!path.Skip(hop).All(remaining => IsOpen(remaining, down)))
			{
				return new StepOutcome(null, true);
			}

			var step = path[hop];
			var key = StepKey(step.Route, current);
			if (!tried.TryGetValue(key, out var outcome))
			{
				outcome = await TakeAsync(step, current, hints, down, cancellationToken);
				tried[key] = outcome;
			}
			if (outcome.Found is not { } found)
			{
				return outcome;
			}
			current = [.. current.Concat(found).Distinct()];
		}

		return current.Any(id => id.Source == target) ? new StepOutcome(current, false) : new StepOutcome(null, false);
	}

	private static async Task<StepOutcome> TakeAsync(
		RouteStep step,
		IReadOnlyList<IExternalMediaId> candidates,
		Optional<ResolutionHints> hints,
		HashSet<MediaProvider> down,
		CancellationToken cancellationToken
	)
	{
		try
		{
			return await step.Converter.ConvertAsync(step.Route, candidates, hints, cancellationToken) switch
			{
				Success<IReadOnlyList<IExternalMediaId>>(var found) => new StepOutcome(found, false),
				Error { Exception: ProviderUnavailableException unavailable } => Unavailable(unavailable, down),
				_ => new StepOutcome(null, false),
			};
		}
		catch (ProviderUnavailableException unavailable)
		{
			Activity.Current?.AddException(unavailable);
			return Unavailable(unavailable, down);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Activity.Current?.AddException(ex);
			return new StepOutcome(null, true);
		}
	}

	private static StepOutcome Unavailable(ProviderUnavailableException unavailable, HashSet<MediaProvider> down)
	{
		down.Add(unavailable.Provider);
		return new StepOutcome(null, true);
	}

	private bool IsOpen(RouteStep step, HashSet<MediaProvider> down) =>
		step.Route.Via.All(provider => !down.Contains(provider) && capacity.Of(provider).IsOpen(clock.Now));

	private static string StepKey(IdRoute route, IReadOnlyList<IExternalMediaId> candidates)
	{
		var from = route.From is Some<MediaProvider>(var source) ? source.ToString() : "hints";
		var inputs = candidates
			.Where(id => route.From is not Some<MediaProvider>(var provider) || id.Source == provider)
			.Select(id => id.ToString())
			.Order(StringComparer.Ordinal);
		return $"{from}>{route.To}:{string.Join(",", inputs)}";
	}

	private readonly record struct StepOutcome(IReadOnlyList<IExternalMediaId>? Found, bool Transient);
}
