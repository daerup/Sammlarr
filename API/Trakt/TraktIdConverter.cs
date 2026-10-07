using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;

namespace API.Trakt;

internal sealed class TraktIdConverter(TraktTitles titles) : IExternalIdConverter
{
	private static readonly ById ByAnyId = new(
		Accepts: [MediaProvider.Trakt, MediaProvider.Imdb, MediaProvider.Tmdb, MediaProvider.Tvdb],
		Yields: [MediaProvider.Trakt, MediaProvider.Imdb, MediaProvider.Tmdb, MediaProvider.Tvdb],
		Cost: 1
	);

	public IReadOnlyList<IdLookup> Lookups { get; } = [ByAnyId];

	public async Task<Result<IdLookupResult>> LookUpAsync(IdLookupRequest request, CancellationToken cancellationToken = default)
	{
		return request switch
		{
			ByIdRequest(var lookup, var id) when lookup == ByAnyId => await FindAsync(id, cancellationToken),
			_ => Error.From("Trakt does not offer this lookup."),
		};
	}

	private Task<Result<IdLookupResult>> FindAsync(IExternalMediaId id, CancellationToken cancellationToken)
	{
		return titles
			.FindAsync(id, cancellationToken)
			.Map(title =>
				title.Match(found => new IdLookupResult(TraktTitle.Ids(found, id.Type), TraktTitle.Hints(found, id.Type)), () => new IdLookupResult([], new None()))
			);
	}
}
