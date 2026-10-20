using Domain.Media;

namespace Contracts.Datasets;

public interface IAnimeIds : IDatasetTable<AnimeIdMapping>
{
	Task<Optional<AnimeIdMapping>> FindAsync(IExternalMediaId id, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<IExternalMediaId, AnimeIdMapping>> FindManyAsync(
		IReadOnlyCollection<IExternalMediaId> ids,
		CancellationToken cancellationToken = default
	);
}

public sealed record AnimeIdMapping(
	MediaType Type,
	Optional<int> AniList,
	Optional<int> Mal,
	Optional<int> Tvdb,
	Optional<int> Tmdb,
	Optional<string> Imdb,
	Optional<int> SeriesTvdb
)
{
	public Optional<int> TvdbSeason { get; init; } = new None();

	public Optional<int> EpisodeOffset { get; init; } = new None();
}
