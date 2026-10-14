using Contracts.Media.Artwork;

namespace UI.Shared.Services;

internal sealed class ArtworkSources(IEnumerable<IArtworkResizer> resizers)
{
	public string? ByWidth(string? url, params IReadOnlyList<ArtworkSize> sizes) =>
		SrcSet(url, [.. sizes.Select(size => (size, FormattableString.Invariant($"{size.Width}w")))]);

	public string Sized(string url, ArtworkSize size) => Resize(url, size).GetValueOr(url);

	public string? ByDensity(string url, ArtworkSize size) => SrcSet(url, [(size, "1x"), (new ArtworkSize(size.Width * 2, size.Height * 2), "2x")]);

	private string? SrcSet(string? url, IEnumerable<(ArtworkSize Size, string Descriptor)> candidates)
	{
		if (url is null)
		{
			return null;
		}

		var sources = candidates
			.SelectMany(candidate => Resize(url, candidate.Size).Match(resized => new[] { (Url: resized, candidate.Descriptor) }, () => []))
			.DistinctBy(source => source.Url)
			.Select(source => $"{source.Url} {source.Descriptor}")
			.ToList();
		return sources.Count == 0 ? null : string.Join(", ", sources);
	}

	private Optional<string> Resize(string url, ArtworkSize size) =>
		resizers.Aggregate<IArtworkResizer, Optional<string>>(new None(), (found, resizer) => found.Or(() => resizer.Resize(url, size)));
}
