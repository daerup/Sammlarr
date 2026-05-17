using System.ComponentModel.DataAnnotations;
using Contracts.Watchlist.Outbox;
using DB.Data.MediaGroup;

namespace DB.Data.Watchlist.Outbox;

internal record WatchlistOutboxEntity : Entity<Guid>
{
	[Required]
	public MediaIdentifierGroupEntity Media { get; set; } = null!;

	[Required]
	public WatchlistOperationKind Kind { get; set; }
}
