using Contracts.Watchlist.Outbox;
using DB.Data.Media;

namespace DB.Data.Watchlist.Outbox;

internal record WatchlistOutboxEntity
{
	public required MediaRow Media { get; set; }

	public required WatchlistOperationKind Kind { get; set; }

	public int Attempts { get; set; }
}
