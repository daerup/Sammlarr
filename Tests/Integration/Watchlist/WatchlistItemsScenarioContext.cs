using Contracts.Media;
using Contracts.Watchlist;

namespace Integration.Tests.Watchlist;

public sealed class WatchlistItemsScenarioContext
{
	public List<WatchlistedMediaDetail>? Items { get; set; }
	public IReadOnlySet<MediaId>? Ids { get; set; }
}
