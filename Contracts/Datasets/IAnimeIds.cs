using Domain.Media;

namespace Contracts.Datasets;

public interface IAnimeIds : IDatasetTable<AnimeIdMapping>
{
	Task<Optional<AnimeIdMapping>> FindAsync(IExternalMediaId id, CancellationToken cancellationToken = default);
}

public sealed record AnimeIdMapping(
	MediaType Type,
	Optional<int> AniList,
	Optional<int> Mal,
	Optional<int> Tvdb,
	Optional<int> Tmdb,
	Optional<string> Imdb,
	Optional<int> SeriesTvdb
);
