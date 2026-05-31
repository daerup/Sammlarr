namespace Integration.Tests.Infrastructure.Media.Plex;

public sealed class RemoteWatchlistState
{
	public HashSet<string> PlexIds { get; } = [];

	public Dictionary<string, ResolvedVersion> Known { get; } = [];
}
