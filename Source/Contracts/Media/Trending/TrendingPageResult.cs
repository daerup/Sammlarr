namespace Contracts.Media.Trending;

public record TrendingPageResult(IReadOnlyList<ITrendingMedia> Items, int TotalPages);
