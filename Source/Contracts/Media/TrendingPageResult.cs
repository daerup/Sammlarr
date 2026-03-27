namespace Contracts.Media;

public sealed record TrendingPageResult(IReadOnlyList<ITrendingMedia> Items, int TotalPages);
