using Contracts.Media.Metadata;
using Contracts.Watchlist;
using Domain.Media.Metadata;

namespace Integration.Tests.Settings;

public sealed class SettingsScenarioContext
{
	public string? RetrievedKey { get; set; }
	public bool? KeyValid { get; set; }
	public IReadOnlyList<MediaRating>? Ratings { get; set; }
	public WatchlistPushAck? PlexPushResult { get; set; }
}
