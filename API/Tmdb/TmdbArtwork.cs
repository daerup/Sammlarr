using Contracts.Media.Artwork;
using Shared.Options;

namespace API.Tmdb;

internal sealed class TmdbArtwork(TmdbOptions options) : IArtworkResizer
{
	private static readonly int[] Widths = [92, 154, 185, 300, 342, 500, 780, 1280];

	private string Base => $"{options.ImageBaseUrl.TrimEnd('/')}/";

	public string? Original(string? path) => string.IsNullOrWhiteSpace(path) ? null : $"{Base}original/{path.TrimStart('/')}";

	public Optional<string> Resize(string url, ArtworkSize size) => FileOf(url).Map(file => WidthFor(size).Map(width => $"{Base}w{width}/{file}"));

	private Optional<string> FileOf(string url)
	{
		var sized = url.StartsWith(Base, StringComparison.Ordinal) ? url[Base.Length..] : string.Empty;
		var slash = sized.IndexOf('/');
		return slash > 0 && slash < sized.Length - 1 ? Some.Of(sized[(slash + 1)..]) : new None();
	}

	private static Optional<int> WidthFor(ArtworkSize size) => Widths.FirstOrNone(width => width >= size.Width);
}
