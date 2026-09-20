using System.ComponentModel.DataAnnotations;
using Domain.Feed;
using Domain.Processing;
using Domain.Providers;

namespace DB.Data.Feed;

internal record FeedRow : Entity<Guid>, IUserOwned
{
	[Required]
	public bool IsEnabled { get; set; }

	[Required]
	public string Name { get; set; }

	[Required]
	public FeedProvider Provider { get; set; }

	[Required]
	public FeedStrategy Strategy { get; set; } = new(new Append());

	[Required]
	public string Identifier { get; set; } = string.Empty;

	public QueryFilter? Filter { get; set; }
	public QuerySort? Sort { get; set; }
	public QueryLimit? Limit { get; set; }
	public FeedRunTrigger Trigger { get; set; } = new(new Manual());
}
