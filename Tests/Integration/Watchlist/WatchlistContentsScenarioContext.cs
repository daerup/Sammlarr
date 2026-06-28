using Contracts.Media;
using Contracts.Media.Search;

namespace Integration.Tests.Watchlist;

public sealed class WatchlistContentsScenarioContext
{
	public List<MediaLookup>? Items { get; set; }
	public IReadOnlySet<MediaId>? Ids { get; set; }
}
