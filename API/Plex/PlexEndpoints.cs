namespace API.Plex;

internal static class PlexEndpoints
{
	public const string RatingKeyParam = "ratingKey";
	public const string SearchQueryParam = "query";

	public const string WatchlistPath = "library/sections/watchlist/all";
	public const string AddToWatchlistPath = "actions/addToWatchlist";
	public const string RemoveFromWatchlistPath = "actions/removeFromWatchlist";
	public const string MetadataPath = "library/metadata";
	public const string SearchPath = "library/search";
	public const string PinsPath = "api/v2/pins";
	public const string UserPath = "api/v2/user";

	public const string Watchlist = $"https://discover.provider.plex.tv/{WatchlistPath}";
	public const string AddToWatchlist = $"https://vod.provider.plex.tv/{AddToWatchlistPath}?{RatingKeyParam}={{0}}";
	public const string RemoveFromWatchlist = $"https://vod.provider.plex.tv/{RemoveFromWatchlistPath}?{RatingKeyParam}={{0}}";
	public const string RatingKeyMetadata = $"https://metadata.provider.plex.tv/{MetadataPath}/{{0}}?includeGuids=1&includeExternalMedia=1";
	public const string Search =
		$"https://discover.provider.plex.tv/{SearchPath}?searchTypes={{0}}&searchProviders=discover&includeMetadata=1&{SearchQueryParam}={{1}}";
	public const string AuthApp = "https://app.plex.tv/auth";
	public const string Pins = $"https://plex.tv/{PinsPath}";
	public const string User = $"https://plex.tv/{UserPath}";
}
