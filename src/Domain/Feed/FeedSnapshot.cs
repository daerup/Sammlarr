namespace Domain.Feed;

public sealed class FeedSnapshot(IReadOnlyList<FeedOverview> feeds)
{
	private readonly Dictionary<FeedId, FeedOverview> _byId = feeds.ToDictionary(overview => overview.Feed.Id);

	public IReadOnlyList<FeedOverview> Feeds { get; } = feeds;

	public Optional<FeedOverview> Find(FeedId id) => _byId.GetValueOrNone(id);
}
