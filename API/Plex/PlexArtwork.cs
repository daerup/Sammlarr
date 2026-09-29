using Contracts.Media.Artwork;

namespace API.Plex;

internal sealed class PlexArtwork(PlexUrls urls) : IArtworkResizer
{
	private const int LargestTranscodedWidth = 900;

	public Optional<string> Resize(string url, ArtworkSize size) =>
		urls.IsArtwork(url) && size.Width <= LargestTranscodedWidth ? Some.Of(urls.Photo(url, size)) : new None();
}
