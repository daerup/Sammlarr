using Domain.Media;

namespace Core.Media;

internal union FetchReason(Uncached, Gaps, Stale);

internal readonly record struct Uncached;

internal readonly record struct Gaps(CachedDetails Stored);

internal readonly record struct Stale(CachedDetails Stored);
