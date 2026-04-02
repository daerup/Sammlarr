namespace Contracts.Media;

public record TrendingPageResult(IReadOnlyList<ITrendingMedia> Items, int TotalPages);
