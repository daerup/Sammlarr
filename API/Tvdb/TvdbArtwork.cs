using Contracts.Media.Artwork;

namespace API.Tvdb;

internal sealed class TvdbArtwork : IArtworkResizer
{
	private const string Banners = "https://artworks.thetvdb.com/banners/";

	private const int ThumbnailWidth = 340;

	public Optional<string> Resize(string url, ArtworkSize size)
	{
		return Optional.When(
			url.StartsWith(Banners, StringComparison.Ordinal) && Path.HasExtension(url),
			() => size.Width <= ThumbnailWidth ? Thumbnail(url) : url
		);
	}

	private static string Thumbnail(string url)
	{
		var extension = url.LastIndexOf('.', StringComparison.Ordinal);
		return $"{url[..extension]}_t{url[extension..]}";
	}
}
