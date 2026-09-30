namespace Harness;

internal sealed class RemoteWantlistState
{
	internal HashSet<string> PlexIds { get; } = [];

	internal Dictionary<string, ResolvedVersion> Known { get; } = [];
}
