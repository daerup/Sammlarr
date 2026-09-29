using Contracts.DI;
using Contracts.Media.Artwork;
using Domain.Providers;

namespace UI.Shared.Services;

internal sealed class ArtworkSources(IKeyedServiceIndex<ArtworkProvider, IArtworkResizer> resizers)
{
	public string? ByWidth(string url, params IReadOnlyList<ArtworkSize> sizes) => SrcSet(url, [.. sizes.Select(size => (size, $"{size.Width}w"))]);

	public string? ByDensity(string url, ArtworkSize size) => SrcSet(url, [(size, "1x"), (new ArtworkSize(size.Width * 2, size.Height * 2), "2x")]);

	private string? SrcSet(string url, IEnumerable<(ArtworkSize Size, string Descriptor)> candidates)
	{
		var sources = candidates
			.SelectMany(candidate => Resize(url, candidate.Size).Match(resized => new[] { (Url: resized, candidate.Descriptor) }, () => []))
			.DistinctBy(source => source.Url)
			.Select(source => $"{source.Url} {source.Descriptor}")
			.ToList();
		return sources.Count == 0 ? null : string.Join(", ", sources);
	}

	private Optional<string> Resize(string url, ArtworkSize size) =>
		Enum.GetValues<ArtworkProvider>()
			.Aggregate<ArtworkProvider, Optional<string>>(new None(), (found, provider) => found.Or(() => resizers[provider].Resize(url, size)));
}
