using Contracts.Media.Metadata;
using Contracts.Watchlist;

namespace Integration.Tests.Settings;

public sealed class ProviderKeyValidationScenarioContext
{
	public IReadOnlyList<MediaRating>? TmdbRatings { get; set; }
	public HttpResponseMessage? MdblistResponse { get; set; }
	public WatchlistPushResult? PlexPushResult { get; set; }
}
