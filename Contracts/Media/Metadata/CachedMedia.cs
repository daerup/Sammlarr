using Domain.Media;

namespace Contracts.Media.Metadata;

public sealed record CachedMedia(IdentifiedMedia Stored, DateTimeOffset? FetchedAt);
