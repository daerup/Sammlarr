using API.Http;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;

namespace API.Plex;

internal sealed class PlexIdConverter(PlexApi plexApi) : IExternalIdConverter
{
	private static readonly ValueSet<MediaProvider> Guids = [MediaProvider.Plex, MediaProvider.Tmdb, MediaProvider.Imdb, MediaProvider.Tvdb];

	private static readonly ById ByGuid = new(
		Accepts: [MediaProvider.Tmdb, MediaProvider.Imdb, MediaProvider.Tvdb],
		Yields: [MediaProvider.Plex],
		Cost: 1,
		GivesHints: false,
		AsksOnce: true
	);

	private static readonly ById ByRatingKey = new(Accepts: [MediaProvider.Plex], Yields: Guids, Cost: 1);

	public IReadOnlyList<IdLookup> Lookups { get; } = [ByGuid, ByRatingKey];

	public Task<Answer<IdLookupResult>> LookUpAsync(IdLookupRequest request, CancellationToken cancellationToken = default)
	{
		return LookUpResultAsync(request, cancellationToken).GuardedAsync(RemoteService.Plex);
	}

	private async Task<Result<IdLookupResult>> LookUpResultAsync(IdLookupRequest request, CancellationToken cancellationToken)
	{
		return request switch
		{
			ByIdRequest(var lookup, var id) when lookup == ByGuid => await ByGuidAsync(id, cancellationToken),
			ByIdRequest(var lookup, PlexId plexId) when lookup == ByRatingKey => await ByRatingKeyAsync(plexId, cancellationToken),
			_ => Error.From("Plex does not offer this lookup."),
		};
	}

	private Task<Result<IdLookupResult>> ByGuidAsync(IExternalMediaId id, CancellationToken cancellationToken)
	{
		return plexApi.MatchAsync(id, cancellationToken).Map(found => Answer(found.Map(plex => (IReadOnlyList<IExternalMediaId>)[plex, id]).GetValueOr([])));
	}

	private Task<Result<IdLookupResult>> ByRatingKeyAsync(PlexId plexId, CancellationToken cancellationToken)
	{
		return plexApi.LookUpExternalsAsync(plexId, cancellationToken).Map(Answer);
	}

	private static IdLookupResult Answer(IReadOnlyList<IExternalMediaId> ids) => new(ids, new None());
}
