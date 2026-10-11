using System.ComponentModel.DataAnnotations.Schema;
using Domain.Providers;

namespace DB.Data.Feed;

[Table("DeletedFeeds")]
internal sealed record DeletedFeedRow : IUserOwned
{
	public required Guid FeedId { get; init; }

	public required string Name { get; init; }

	public required FeedProvider Provider { get; init; }

	public required DateTimeOffset DeletedAt { get; init; }
}
