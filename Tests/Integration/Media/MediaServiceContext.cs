using Domain.Media;

namespace Integration.Tests.Media;

public sealed class MediaServiceContext
{
	public MediaLookup? Detail { get; set; }
	public MediaIdentity? Identification { get; set; }
	public IReadOnlyList<MediaIdentity?>? Identifications { get; set; }
	public Dictionary<string, MediaLookup?> DetailsByKey { get; } = new();
}
