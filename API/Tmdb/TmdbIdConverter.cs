using System.Globalization;
using API.Tmdb.Types;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;

namespace API.Tmdb;

internal class TmdbIdConverter(TmdbApi tmdbApi) : IExternalIdConverter
{
	private static readonly ById ByTmdbId = new(Accepts: [MediaProvider.Tmdb], Yields: [MediaProvider.Imdb, MediaProvider.Tvdb], Cost: 1);

	private static readonly ById Find = new(Accepts: [MediaProvider.Imdb, MediaProvider.Tvdb], Yields: [MediaProvider.Tmdb], Cost: 1);

	public MediaProvider Provider => MediaProvider.Tmdb;

	public IReadOnlyList<IdLookup> Lookups { get; } = [ByTmdbId, Find];

	public async Task<Result<IdLookupResult>> LookUpAsync(IdLookupRequest request, CancellationToken cancellationToken = default)
	{
		return request switch
		{
			ByIdRequest(var lookup, TmdbId tmdbId) when lookup == ByTmdbId => await DetailAsync(tmdbId, cancellationToken),
			ByIdRequest(var lookup, var id) when lookup == Find => await FindAsync(id, cancellationToken),
			_ => Error.From("TMDB does not offer this lookup."),
		};
	}

	private Task<Result<IdLookupResult>> DetailAsync(TmdbId tmdbId, CancellationToken cancellationToken)
	{
		return tmdbApi.GetDetailWithExternalIdsAsync(tmdbId, cancellationToken).Map(detail => FoundByDetail(detail, tmdbId));
	}

	private Task<Result<IdLookupResult>> FindAsync(IExternalMediaId id, CancellationToken cancellationToken)
	{
		return tmdbApi.FindAsync(id, cancellationToken).Map(item => FoundByFind(item, id));
	}

	private static IdLookupResult FoundByDetail(TmdbTypes.Details detail, TmdbId tmdbId)
	{
		return new IdLookupResult([.. IdsOf(detail.ExternalIds, tmdbId.Type)], HintsOf(detail.OriginalTitle ?? detail.OriginalName, tmdbId.Type));
	}

	private static IdLookupResult FoundByFind(TmdbTypes.FindItem item, IExternalMediaId id)
	{
		IReadOnlyList<IExternalMediaId> ids = [TmdbId.From(item.Id.ToString(CultureInfo.InvariantCulture), id.Type)];
		return new IdLookupResult(ids, HintsOf(item.OriginalTitle ?? item.OriginalName, id.Type));
	}

	private static IEnumerable<IExternalMediaId> IdsOf(TmdbTypes.ExternalIds? externals, MediaType type)
	{
		if (!string.IsNullOrWhiteSpace(externals?.ImdbId))
		{
			yield return ImdbId.From(externals.ImdbId, type);
		}

		if (externals?.TvdbId is > 0)
		{
			yield return TvdbId.From(externals.TvdbId.Value.ToString(CultureInfo.InvariantCulture), type);
		}
	}

	private static Optional<IdentifyHints> HintsOf(string? originalTitle, MediaType type)
	{
		return Optional
			.Of(originalTitle)
			.Map(title => new IdentifyHints
			{
				Title = title,
				Year = 0,
				MediaType = type,
			});
	}
}
