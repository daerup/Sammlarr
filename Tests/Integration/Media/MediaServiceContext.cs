namespace Integration.Tests.Media;

public sealed class MediaServiceContext
{
	public MediaMatch? Detail { get; set; }
	public MediaIdentity? Identification { get; set; }
	public IReadOnlyList<MediaIdentity?>? Identifications { get; set; }
	public Dictionary<string, MediaMatch?> DetailsByKey { get; } = new();
}
