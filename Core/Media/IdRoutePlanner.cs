using Contracts.Media.Conversions;
using Domain.Providers;

namespace Core.Media;

internal readonly record struct RouteStep(IExternalIdConverter Converter, IdRoute Route);

internal sealed record IdRoutePlan(IReadOnlyList<IReadOnlyList<RouteStep>> Paths, bool Blocked);

internal static class IdRoutePlanner
{
	private const int MaxHops = 3;

	internal static IdRoutePlan Plan(
		IReadOnlySet<MediaProvider> have,
		bool hasHints,
		MediaProvider target,
		IReadOnlyList<RouteStep> steps,
		Func<MediaProvider, ProviderLoad> load,
		DateTimeOffset now
	)
	{
		var paths = new List<IReadOnlyList<RouteStep>>();
		Walk([], have, hasHints, target, steps, paths);

		var open = paths.Where(path => IsOpen(path, load, now)).ToList();
		return new IdRoutePlan([.. open.OrderBy(path => Weight(path, load)).ThenBy(path => path.Count)], open.Count < paths.Count);
	}

	private static bool IsOpen(IReadOnlyList<RouteStep> path, Func<MediaProvider, ProviderLoad> load, DateTimeOffset now) =>
		path.All(step => step.Route.Via.All(provider => load(provider).IsOpen(now)));

	private static void Walk(
		IReadOnlyList<RouteStep> path,
		IReadOnlySet<MediaProvider> reached,
		bool hasHints,
		MediaProvider target,
		IReadOnlyList<RouteStep> steps,
		List<IReadOnlyList<RouteStep>> paths
	)
	{
		if (path.Count == MaxHops)
		{
			return;
		}

		foreach (var step in steps)
		{
			var route = step.Route;
			var applies = (route.From, path.Count) switch
			{
				(Some<MediaProvider>(var from), 0) => reached.Contains(from),
				(Some<MediaProvider>(var from), _) => from == path[^1].Route.To,
				(_, 0) => hasHints,
				_ => false,
			};
			if (!applies)
			{
				continue;
			}

			if (route.From is Some<MediaProvider>(var same) && same == route.To)
			{
				if (path.Count == 0 && route.To == target)
				{
					paths.Add([step]);
				}
				continue;
			}

			if (reached.Contains(route.To))
			{
				continue;
			}

			IReadOnlyList<RouteStep> next = [.. path, step];
			if (route.To == target)
			{
				paths.Add(next);
				continue;
			}

			Walk(next, new HashSet<MediaProvider>(reached) { route.To }, hasHints, target, steps, paths);
		}
	}

	private static double Weight(IReadOnlyList<RouteStep> path, Func<MediaProvider, ProviderLoad> load) =>
		path.Sum(step => step.Route.Calls * (1 + step.Route.Via.Sum(provider => load(provider).Busy)));
}
