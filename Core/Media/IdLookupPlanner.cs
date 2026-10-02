using Contracts.Media.Conversions;
using Contracts.Providers;
using Domain.Media.Conversions;
using Domain.Providers;

namespace Core.Media;

internal sealed record IdLookupPlan(IReadOnlyList<IReadOnlyList<Hop>> Paths, bool Blocked);

internal static class IdLookupPlanner
{
	private const int MaxHops = 3;

	internal static IReadOnlyList<Hop> Derive(IEnumerable<IExternalIdConverter> converters)
	{
		return [.. converters.SelectMany(converter => converter.Lookups.SelectMany(lookup => HopsOf(converter, lookup)))];
	}

	internal static IdLookupPlan Plan(
		IReadOnlySet<MediaProvider> have,
		bool hasHints,
		MediaProvider target,
		IReadOnlyList<Hop> hops,
		IProviderCapacity capacity,
		DateTimeOffset now
	)
	{
		if (have.Contains(target))
		{
			return PlanPresent(target, hops, capacity, now);
		}

		var paths = new List<IReadOnlyList<Hop>>();
		Walk([], have, hasHints, target, hops, paths);

		var open = paths.Where(path => path.All(hop => capacity.Of(hop.Converter.Provider).IsOpen(now))).ToList();
		return new IdLookupPlan([.. open.OrderBy(path => Weight(path, capacity)).ThenBy(path => path.Count)], open.Count < paths.Count);
	}

	private static IEnumerable<Hop> HopsOf(IExternalIdConverter converter, IdLookup lookup)
	{
		return lookup switch
		{
			ById byId => IdHops(converter, byId),
			ByHints byHints => byHints.Yields.Order().Select(to => (Hop)new HintsToId(converter, byHints, to)),
		};
	}

	private static IEnumerable<Hop> IdHops(IExternalIdConverter converter, ById byId)
	{
		foreach (var from in byId.Accepts.Order())
		{
			foreach (var to in byId.Yields.Order().Where(to => to != from || to == converter.Provider))
			{
				yield return new IdToId(converter, byId, from, to);
			}

			yield return new IdToHints(converter, byId, from);
		}
	}

	private static IdLookupPlan PlanPresent(MediaProvider target, IReadOnlyList<Hop> hops, IProviderCapacity capacity, DateTimeOffset now)
	{
		var self = hops.Where(hop => hop is IdToId id && id.From == target && id.To == target).ToList();
		if (self.Count == 0)
		{
			return new IdLookupPlan(
				[
					[],
				],
				false
			);
		}

		var open = self.Where(hop => capacity.Of(hop.Converter.Provider).IsOpen(now)).ToList();
		return new IdLookupPlan([.. open.Select(hop => (IReadOnlyList<Hop>)[hop])], open.Count < self.Count);
	}

	private static void Walk(
		IReadOnlyList<Hop> path,
		IReadOnlySet<MediaProvider> reached,
		bool hasHints,
		MediaProvider target,
		IReadOnlyList<Hop> hops,
		List<IReadOnlyList<Hop>> paths
	)
	{
		if (path.Count == MaxHops)
		{
			return;
		}

		foreach (var hop in hops)
		{
			var to = To(hop);
			if (!Starts(hop, path, reached, hasHints) || to.When(reached.Contains).IsSome())
			{
				continue;
			}

			IReadOnlyList<Hop> next = [.. path, hop];
			if (to.When(provider => provider == target).IsSome())
			{
				paths.Add(next);
				continue;
			}

			var reachedNext = new HashSet<MediaProvider>(reached);
			to.Apply(provider => reachedNext.Add(provider));
			if (hop is HintsToId)
			{
				reachedNext.Add(hop.Converter.Provider);
			}

			Walk(next, reachedNext, hasHints, target, hops, paths);
		}
	}

	private static bool Starts(Hop hop, IReadOnlyList<Hop> path, IReadOnlySet<MediaProvider> reached, bool hasHints)
	{
		var first = path.Count == 0;
		return hop switch
		{
			HintsToId search => !reached.Contains(search.Converter.Provider) && (first ? hasHints : path[^1] is IdToHints),
			IdToId id => first ? reached.Contains(id.From) : Follows(hop, id.From, path[^1]),
			IdToHints hints => first ? reached.Contains(hints.From) : Follows(hop, hints.From, path[^1]),
		};
	}

	private static bool Follows(Hop hop, MediaProvider from, Hop previous)
	{
		return previous switch
		{
			IdToId id => id.To == from,
			HintsToId search => search.To == from && search.Converter.Provider != hop.Converter.Provider,
			IdToHints => false,
		};
	}

	private static Optional<MediaProvider> To(Hop hop)
	{
		return hop switch
		{
			IdToId id => Some.Of(id.To),
			HintsToId search => Some.Of(search.To),
			IdToHints => new None(),
		};
	}

	private static double Weight(IReadOnlyList<Hop> path, IProviderCapacity capacity)
	{
		return path.Sum(hop => hop.Cost * (1 + capacity.Of(hop.Converter.Provider).Busy));
	}
}
