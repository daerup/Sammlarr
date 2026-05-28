using Contracts.Media;

namespace Integration.Tests.Infrastructure.Media;

public sealed class CachedMediaContext
{
	public Dictionary<string, MediaId> MediaIds { get; } = new();
}
