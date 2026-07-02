using Contracts.Media;
using Contracts.Media.Search;
using Domain.Media;

namespace Integration.Tests.Watchlist;

public sealed class WatchlistContentsScenarioContext
{
	public List<MediaLookup>? Items { get; set; }
	public IReadOnlySet<MediaId>? Ids { get; set; }
}
