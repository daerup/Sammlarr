namespace Harness;

internal sealed class RemoteWatchlistState
{
	internal HashSet<string> PlexIds { get; } = [];

	internal Dictionary<string, ResolvedVersion> Known { get; } = [];
}
