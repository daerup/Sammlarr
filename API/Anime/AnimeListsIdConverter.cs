using System.Globalization;
using Contracts.Datasets;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;

namespace API.Anime;

internal sealed class AnimeListsIdConverter(IAnimeIds map, IDatasetLoads loads) : IExternalIdConverter
{
	private static readonly ById ByAnimeId = new(
		Accepts: [MediaProvider.AniList, MediaProvider.Mal],
		Yields: [MediaProvider.AniList, MediaProvider.Mal, MediaProvider.Tvdb, MediaProvider.Tmdb, MediaProvider.Imdb],
		Cost: 0,
		GivesHints: false
	);

	private static readonly ById ByShowId = new(
		Accepts: [MediaProvider.Tvdb, MediaProvider.Tmdb, MediaProvider.Imdb],
		Yields: [MediaProvider.AniList, MediaProvider.Mal],
		Cost: 0,
		GivesHints: false
	);

	internal static readonly Error NotLoaded = Error.From("The anime id map has not been loaded yet.", new RemoteUnavailableException(RemoteService.AnimeLists));

	public IReadOnlyList<IdLookup> Lookups { get; } = [ByAnimeId, ByShowId];

	public async Task<Result<IdLookupResult>> LookUpAsync(IdLookupRequest request, CancellationToken cancellationToken = default)
	{
		return request switch
		{
			ByIdRequest(var lookup, var id) when lookup == ByAnimeId || lookup == ByShowId => await FindAsync(lookup, id, cancellationToken),
			_ => Error.From("The anime id map does not offer this lookup."),
		};
	}

	private async Task<Result<IdLookupResult>> FindAsync(ById lookup, IExternalMediaId id, CancellationToken cancellationToken)
	{
		var loaded = await loads.FindAsync(DatasetNames.AnimeIds, cancellationToken);
		return await loaded.ToResult(NotLoaded).Map(_ => AnswerAsync(lookup, id, cancellationToken));
	}

	private async Task<IdLookupResult> AnswerAsync(ById lookup, IExternalMediaId id, CancellationToken cancellationToken)
	{
		var entry = await map.FindAsync(id, cancellationToken);
		var ids = entry.Map(found => Ids(found, id.Type is MediaType.Unknown ? found.Type : id.Type)).GetValueOr([]);
		return new IdLookupResult([.. ids.Where(each => lookup.Yields.Contains(each.Source))], new None());
	}

	internal static IReadOnlyList<IExternalMediaId> Ids(AnimeIdMapping entry, MediaType type)
	{
		Optional<IExternalMediaId>[] found =
		[
			entry.AniList.Map(value => Id(AniListId.From(Number(value), type))),
			entry.Mal.Map(value => Id(MalId.From(Number(value), type))),
			entry.Tvdb.Map(value => Id(TvdbId.From(Number(value), type))),
			entry.Tmdb.Map(value => Id(TmdbId.From(Number(value), type))),
			entry.Imdb.Map(value => Id(ImdbId.From(value, type))),
		];
		return [.. found.WhereSome()];
	}

	private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

	private static IExternalMediaId Id(IExternalMediaId id) => id;
}
