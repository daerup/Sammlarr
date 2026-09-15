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

	public const string SearchQuery = $"{SearchPath}?searchTypes={{0}}&searchProviders=discover&includeMetadata=1&limit={{2}}&{SearchQueryParam}={{1}}";
	public const string RatingKeyMetadataQuery = $"{MetadataPath}/{{0}}?includeGuids=1&includeExternalMedia=1";
	public const string AddToWatchlistQuery = $"{AddToWatchlistPath}?{RatingKeyParam}={{0}}";
	public const string RemoveFromWatchlistQuery = $"{RemoveFromWatchlistPath}?{RatingKeyParam}={{0}}";
}
