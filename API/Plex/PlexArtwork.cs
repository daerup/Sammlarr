using Contracts.Media.Artwork;

namespace API.Plex;

internal sealed class PlexArtwork(PlexUrls urls) : IArtworkResizer
{
	private const int LargestTranscodedWidth = 900;

	public Optional<string> Resize(string url, ArtworkSize size)
	{
		return Optional.When(urls.IsArtwork(url) && size.Width <= LargestTranscodedWidth, () => urls.Photo(url, size));
	}
}
