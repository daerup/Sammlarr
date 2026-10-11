using Domain.Media;

namespace Domain.Feed.Run;

public enum FeedRunItemState
{
	Added,
	Kept,
	Removed,
	Unaddressable,
}

public sealed record FeedRunItem
{
	public required MediaId MediaId { get; init; }
	public required FeedRunItemState State { get; init; }
}
