namespace Contracts.Media.Artwork;

public interface IArtworkResizer
{
	Optional<string> Resize(string url, ArtworkSize size);
}
