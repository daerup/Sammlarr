using Domain.Providers;

namespace Domain.Feed.Trace;

public sealed record RunLayout(
	IReadOnlyList<MediaProvider> Targets,
	IReadOnlyList<LayoutNode> Ids,
	IReadOnlyList<RemoteService> Details,
	IReadOnlyList<LayoutEdge> Edges
)
{
	public static RunLayout Empty { get; } = new([], [], [], []);

	public SinkKind Sink { get; init; }
}

public readonly record struct LayoutNode(RemoteService Service, int Column, bool InMemory);

public readonly record struct LayoutEdge(Optional<RemoteService> From, RemoteService To);
