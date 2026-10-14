namespace UI.Features.Feeds.Pages;

internal static class FeedPaging
{
	private const int PageSize = 20;

	internal static int PageCount(int feedCount) => Math.Max(1, (int)Math.Ceiling(feedCount / (double)PageSize));

	internal static int PageWithin(int page, int feedCount) => Math.Clamp(page, 1, PageCount(feedCount));

	internal static IReadOnlyList<T> Slice<T>(IReadOnlyList<T> feeds, int page) => [.. feeds.Skip((page - 1) * PageSize).Take(PageSize)];
}
