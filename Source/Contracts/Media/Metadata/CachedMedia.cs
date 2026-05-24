using Contracts.Media.Search;

namespace Contracts.Media.Metadata;

public sealed record CachedMedia(IMediaDetail Detail, DateTimeOffset? FetchedAt);
