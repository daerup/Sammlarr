using DB.Data.Media;
using Domain.Watchlist.Outbox;

namespace DB.Data.Watchlist.Outbox;

internal record WatchlistOutboxRow : IUserOwned
{
	public required MediaRow Media { get; set; }

	public required WatchlistOperationKind Kind { get; set; }

	public int Attempts { get; set; }
}
