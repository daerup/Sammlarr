using Contracts.Providers;
using Core.Media;
using Domain.Feed.Trace;
using Domain.Providers;

namespace Core.Feed;

internal static class RunLayouts
{
	private static readonly IRemoteCapacity Unlimited = new Open();

	internal static RunLayout Build(
		IReadOnlyList<Hop> hops,
		IReadOnlySet<MediaProvider> sources,
		IReadOnlyList<MediaProvider> targets,
		IReadOnlyList<RemoteService> enrichers
	)
	{
		var paths = targets.SelectMany(target => IdLookupPlanner.Plan(sources, true, target, hops, Unlimited, DateTimeOffset.UnixEpoch).Paths).ToList();
		IReadOnlyList<LayoutEdge> edges = [.. paths.SelectMany(Edges).Distinct()];
		var columns = Columns(Ranks(paths), edges);
		var free = hops.Where(hop => hop.Cost == 0 && Accepts(hop, sources)).Select(hop => hop.Remote).Distinct().Where(remote => !columns.ContainsKey(remote));
		IReadOnlyList<LayoutNode> nodes =
		[
			.. columns.Select(pair => new LayoutNode(pair.Key, pair.Value, false)),
			.. free.Select(remote => new LayoutNode(remote, 1, true)),
		];
		var placed = new Dictionary<RemoteService, Placed>();
		var ordered = new List<LayoutNode>();
		foreach (var column in nodes.GroupBy(node => node.Column).OrderBy(group => group.Key))
		{
			var rows = column.OrderBy(node => Barycenter(node.Service, edges, placed)).ThenBy(node => node.Service).ToList();
			foreach (var (index, node) in rows.Index())
			{
				placed[node.Service] = new Placed(node.Column, rows.Count == 1 ? 0.5 : (double)index / (rows.Count - 1));
			}

			ordered.AddRange(rows);
		}

		IReadOnlyList<RemoteService> details =
		[
			.. enrichers.OrderBy(enricher => placed.GetValueOrNone(enricher).GetValueOr(Placed.Unplaced)).ThenBy(enricher => enricher),
		];
		return new RunLayout(targets, ordered, details, edges);
	}

	private static double Barycenter(RemoteService service, IReadOnlyList<LayoutEdge> edges, IReadOnlyDictionary<RemoteService, Placed> placed)
	{
		return edges
			.Where(edge => edge.To == service)
			.Select(edge => edge.From.Match(from => placed.GetValueOrNone(from).Map(spot => spot.Row), () => Some.Of(0.5)))
			.WhereSome()
			.DefaultIfEmpty(0.5)
			.Average();
	}

	private readonly record struct Placed(int Column, double Row) : IComparable<Placed>
	{
		internal static Placed Unplaced => new(int.MaxValue, 0);

		public int CompareTo(Placed other) => (Column, Row).CompareTo((other.Column, other.Row));
	}

	private static Dictionary<RemoteService, int> Ranks(IEnumerable<IReadOnlyList<Hop>> paths)
	{
		return paths
			.SelectMany(path => path.Select((hop, index) => (hop.Remote, Index: index)))
			.GroupBy(placed => placed.Remote)
			.OrderBy(group => group.Min(placed => placed.Index))
			.ThenBy(group => group.Key)
			.Select((group, rank) => (group.Key, Rank: rank))
			.ToDictionary(ranked => ranked.Key, ranked => ranked.Rank);
	}

	private static Dictionary<RemoteService, int> Columns(Dictionary<RemoteService, int> ranks, IReadOnlyList<LayoutEdge> edges)
	{
		var columns = new Dictionary<RemoteService, int>();
		foreach (var service in ranks.OrderBy(pair => pair.Value).Select(pair => pair.Key))
		{
			var before = edges.Where(edge => edge.To == service).Select(edge => edge.From).WhereSome().Where(from => ranks[from] < ranks[service]);
			columns[service] = 1 + before.Select(from => columns[from]).DefaultIfEmpty(0).Max();
		}

		return columns;
	}

	private static IEnumerable<LayoutEdge> Edges(IReadOnlyList<Hop> path)
	{
		return path.Select((hop, index) => new LayoutEdge(Optional.When(index > 0, () => path[index - 1].Remote), hop.Remote))
			.Where(edge => edge.From.Match(from => from != edge.To, () => true));
	}

	private static bool Accepts(Hop hop, IReadOnlySet<MediaProvider> sources)
	{
		return hop is IdToId id && sources.Contains(id.From);
	}

	private sealed class Open : IRemoteCapacity
	{
		public RemoteLoad Of(RemoteService remote) => new(new None(), 0);
	}
}
