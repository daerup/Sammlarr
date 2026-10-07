using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;

namespace API.Mdblist;

internal sealed class MdblistIdConverter(MdblistTitles titles) : IExternalIdConverter
{
	private static readonly ById ByAnyId = new(
		Accepts: [MediaProvider.Imdb, MediaProvider.Tmdb, MediaProvider.Tvdb, MediaProvider.Trakt, MediaProvider.Mal],
		Yields: [MediaProvider.Imdb, MediaProvider.Tmdb, MediaProvider.Tvdb, MediaProvider.Trakt, MediaProvider.Mal, MediaProvider.Mdblist],
		Cost: 2
	);

	public IReadOnlyList<IdLookup> Lookups { get; } = [ByAnyId];

	public async Task<Result<IdLookupResult>> LookUpAsync(IdLookupRequest request, CancellationToken cancellationToken = default)
	{
		return request switch
		{
			ByIdRequest(var lookup, var id) when lookup == ByAnyId => await FindAsync(id, cancellationToken),
			_ => Error.From("MDBList does not offer this lookup."),
		};
	}

	private Task<Result<IdLookupResult>> FindAsync(IExternalMediaId id, CancellationToken cancellationToken)
	{
		return titles
			.FindAsync(id, cancellationToken)
			.Map(title => title.Match(found => MdblistIds.Found(found, id.Type), () => new IdLookupResult([], new None())));
	}
}
