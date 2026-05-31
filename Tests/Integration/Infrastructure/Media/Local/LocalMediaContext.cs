using Contracts.Media;

namespace Integration.Tests.Infrastructure.Media.Local;

public sealed class LocalMediaContext
{
	public Dictionary<string, MediaId> MediaIds { get; } = new();

	public Dictionary<string, IReadOnlyDictionary<MediaProvider, string>> Ids { get; } = new();
}
