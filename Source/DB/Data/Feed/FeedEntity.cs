using System.ComponentModel.DataAnnotations;
using Contracts.Feed;
using Contracts.Processing;

namespace DB.Data.Feed;

internal record FeedEntity : Entity<Guid>
{
	[Required]
	public bool IsEnabled { get; set; }

	[Required]
	public string Name { get; set; }

	[Required]
	public FeedProvider Provider { get; set; }

	[Required]
	public FeedAddStrategy Strategy { get; set; }

	[Required]
	public string Identifier { get; set; } = string.Empty;

	public QueryFilter? Filter { get; set; }
	public QuerySort? Sort { get; set; }
	public QueryLimit? Limit { get; set; }
	public SyncMode SyncMode { get; set; }
}
