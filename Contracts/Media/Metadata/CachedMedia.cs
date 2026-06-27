namespace Contracts.Media.Metadata;

public sealed record CachedMedia(KnownMedia Stored, DateTimeOffset? FetchedAt);
