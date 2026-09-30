using DB.Data.Feed;
using DB.Data.Feed.Run;
using DB.Data.Media;
using Domain.Providers;
using Domain.Wantlist;

namespace DB.Data.Wantlist;

internal record WantlistRow : IAuditable, IUserOwned
{
	public required MediaRow Media { get; set; }

	public required WantlistItemOrigin Origin { get; set; }

	public SinkKind? Sink { get; set; }

	public ManualSource? ManualSource { get; set; }

	public Guid? SourceFeedId { get; set; }
	public FeedRow? SourceFeed { get; set; }
	public string? SourceFeedNameSnapshot { get; set; }
	public Guid? SourceRunId { get; set; }
	public FeedRunRow? SourceRun { get; set; }

	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }
}
