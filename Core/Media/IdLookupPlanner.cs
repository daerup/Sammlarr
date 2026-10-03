using Contracts.Media.Conversions;
using Contracts.Providers;
using Domain.Media.Conversions;
using Domain.Providers;

namespace Core.Media;

internal sealed record IdLookupPlan(IReadOnlyList<IReadOnlyList<Hop>> Paths);

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

		var paths = new PathSearch(hasHints, target, hops).From([], have).ToList();
		return new IdLookupPlan(Ordered(paths, capacity, now));
	}

	private static IReadOnlyList<IReadOnlyList<Hop>> Ordered(IReadOnlyList<IReadOnlyList<Hop>> paths, IProviderCapacity capacity, DateTimeOffset now)
	{
		var byOpen = paths.ToLookup(path => IsOpen(path, capacity, now));
		var open = byOpen[true].OrderBy(path => Weight(path, capacity)).ThenBy(path => path.Count);
		var blocked = byOpen[false].OrderBy(path => FreeAt(path, capacity)).ThenBy(path => Weight(path, capacity)).ThenBy(path => path.Count);
		return [.. open, .. blocked];
	}

	private static bool IsOpen(IReadOnlyList<Hop> path, IProviderCapacity capacity, DateTimeOffset now)
	{
		return path.All(hop => capacity.Of(hop.Converter.Provider).IsOpen(now));
	}

	private static DateTimeOffset FreeAt(IReadOnlyList<Hop> path, IProviderCapacity capacity)
	{
		return path.Max(hop => capacity.Of(hop.Converter.Provider).BlockedUntil.GetValueOr(DateTimeOffset.MinValue));
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
		IReadOnlyList<IReadOnlyList<Hop>> self =
		[
			.. hops.Where(hop => hop is IdToId id && id.From == target && id.To == target).Select(hop => (IReadOnlyList<Hop>)[hop]),
		];
		return self.Count == 0
			? new IdLookupPlan([
				[],
			])
			: new IdLookupPlan(Ordered(self, capacity, now));
	}

	private static HashSet<MediaProvider> Reached(IReadOnlySet<MediaProvider> reached, Hop hop)
	{
		var next = new HashSet<MediaProvider>(reached);
		To(hop).Apply(provider => next.Add(provider));
		if (hop is HintsToId)
		{
			next.Add(hop.Converter.Provider);
		}

		return next;
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

	private sealed record PathSearch(bool HasHints, MediaProvider Target, IReadOnlyList<Hop> Hops)
	{
		internal IEnumerable<IReadOnlyList<Hop>> From(IReadOnlyList<Hop> path, IReadOnlySet<MediaProvider> reached)
		{
			if (path.Count == MaxHops)
			{
				return [];
			}

			return Hops.Where(hop => Starts(hop, path, reached, HasHints))
				.Where(hop => To(hop).Match(provider => !reached.Contains(provider), () => true))
				.SelectMany(hop => Extend([.. path, hop], hop, reached));
		}

		private IEnumerable<IReadOnlyList<Hop>> Extend(IReadOnlyList<Hop> next, Hop hop, IReadOnlySet<MediaProvider> reached)
		{
			return To(hop).Match(provider => provider == Target, () => false) ? [next] : From(next, Reached(reached, hop));
		}
	}
}
