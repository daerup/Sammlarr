using System.Globalization;
using Contracts.Media.Artwork;
using Microsoft.AspNetCore.WebUtilities;
using Shared.Options;

namespace API.Plex;

internal sealed class PlexUrls(PlexOptions options)
{
	public string Watchlist => Compose(options.DiscoverUrl, PlexEndpoints.WatchlistPath);

	public string Pins => Compose(options.BaseUrl, PlexEndpoints.PinsPath);

	public string User => Compose(options.BaseUrl, PlexEndpoints.UserPath);

	public string AuthApp => options.AuthAppUrl;

	public string Pin(int pinId) => $"{Pins}/{pinId.ToString(CultureInfo.InvariantCulture)}";

	public string Search(string searchTypes, string title, int limit)
	{
		var query = new Dictionary<string, string?>
		{
			["searchTypes"] = searchTypes,
			["searchProviders"] = "discover",
			["includeMetadata"] = "1",
			["limit"] = limit.ToString(CultureInfo.InvariantCulture),
			[PlexEndpoints.SearchQueryParam] = title,
		};
		return Compose(options.DiscoverUrl, QueryHelpers.AddQueryString(PlexEndpoints.SearchPath, query));
	}

	public string Match(int type, string guid)
	{
		var query = new Dictionary<string, string?> { ["type"] = type.ToString(CultureInfo.InvariantCulture), ["guid"] = guid };
		return Compose(options.DiscoverUrl, QueryHelpers.AddQueryString(PlexEndpoints.MatchesPath, query));
	}

	public string RatingKeyMetadata(string plexId)
	{
		var query = new Dictionary<string, string?> { ["includeGuids"] = "1", ["includeExternalMedia"] = "1" };
		return Compose(options.MetadataUrl, QueryHelpers.AddQueryString($"{PlexEndpoints.MetadataPath}/{Uri.EscapeDataString(plexId)}", query));
	}

	public string AddToWatchlist(string ratingKey)
	{
		return Compose(options.VodUrl, QueryHelpers.AddQueryString(PlexEndpoints.AddToWatchlistPath, PlexEndpoints.RatingKeyParam, ratingKey));
	}

	public string RemoveFromWatchlist(string ratingKey)
	{
		return Compose(options.VodUrl, QueryHelpers.AddQueryString(PlexEndpoints.RemoveFromWatchlistPath, PlexEndpoints.RatingKeyParam, ratingKey));
	}

	public bool IsArtwork(string url) => url.StartsWith($"{options.ArtworkUrl.TrimEnd('/')}/", StringComparison.Ordinal);

	public string Photo(string artworkUrl, ArtworkSize size)
	{
		var query = new Dictionary<string, string?>
		{
			["width"] = size.Width.ToString(CultureInfo.InvariantCulture),
			["height"] = size.Height.ToString(CultureInfo.InvariantCulture),
			["minSize"] = "1",
			["url"] = artworkUrl,
		};
		return Compose(options.ImagesUrl, QueryHelpers.AddQueryString(PlexEndpoints.PhotoPath, query));
	}

	private static string Compose(string host, string path) => $"{host.TrimEnd('/')}/{path}";
}
