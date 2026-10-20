namespace API.Plex;

internal static class PlexEndpoints
{
	public const string RatingKeyParam = "ratingKey";
	public const string SearchQueryParam = "query";

	public const string WatchlistPath = "library/sections/watchlist/all";
	public const string AddToWatchlistPath = "actions/addToWatchlist";
	public const string RemoveFromWatchlistPath = "actions/removeFromWatchlist";
	public const string MetadataPath = "library/metadata";
	public const string MatchesPath = "library/metadata/matches";
	public const string SearchPath = "library/search";
	public const string PinsPath = "api/v2/pins";
	public const string UserPath = "api/v2/user";
	public const string PhotoPath = "photo";
}
