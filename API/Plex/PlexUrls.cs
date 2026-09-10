using System.Globalization;
using Shared.Options;

namespace API.Plex;

internal sealed class PlexUrls(PlexOptions options)
{
	public string Watchlist => Compose(options.DiscoverUrl, PlexEndpoints.WatchlistPath);

	public string Pins => Compose(options.BaseUrl, PlexEndpoints.PinsPath);

	public string User => Compose(options.BaseUrl, PlexEndpoints.UserPath);

	public string AuthApp => options.AuthAppUrl;

	public string Pin(int pinId) => $"{Pins}/{pinId.ToString(CultureInfo.InvariantCulture)}";

	public string Search(string searchTypes, string title) =>
		Compose(
			options.DiscoverUrl,
			string.Format(CultureInfo.InvariantCulture, PlexEndpoints.SearchQuery, Uri.EscapeDataString(searchTypes), Uri.EscapeDataString(title))
		);

	public string RatingKeyMetadata(string plexId) =>
		Compose(options.MetadataUrl, string.Format(CultureInfo.InvariantCulture, PlexEndpoints.RatingKeyMetadataQuery, Uri.EscapeDataString(plexId)));

	public string AddToWatchlist(string ratingKey) =>
		Compose(options.VodUrl, string.Format(CultureInfo.InvariantCulture, PlexEndpoints.AddToWatchlistQuery, Uri.EscapeDataString(ratingKey)));

	public string RemoveFromWatchlist(string ratingKey) =>
		Compose(options.VodUrl, string.Format(CultureInfo.InvariantCulture, PlexEndpoints.RemoveFromWatchlistQuery, Uri.EscapeDataString(ratingKey)));

	private static string Compose(string host, string path) => $"{host.TrimEnd('/')}/{path}";
}
