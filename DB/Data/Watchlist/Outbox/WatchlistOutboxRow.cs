using DB.Data.Media;
using Domain.Watchlist.Outbox;

namespace DB.Data.Watchlist.Outbox;

internal record WatchlistOutboxRow : IUserOwned
{
	public required MediaRow Media { get; set; }

	public required WatchlistOperationAction Action { get; set; }

	public WatchlistDeliveryStatus Status { get; set; }

	public int Attempts { get; set; }
}
