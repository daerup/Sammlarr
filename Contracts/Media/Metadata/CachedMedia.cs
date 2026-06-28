namespace Contracts.Media.Metadata;

public sealed record CachedMedia(MediaMatch Stored, DateTimeOffset? FetchedAt);
