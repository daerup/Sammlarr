using API.Http;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;

namespace API.Tvmaze;

internal sealed class TvmazeIdConverter(TvmazeApi tvmaze) : IExternalIdConverter
{
	private static readonly ById ByShowId = new(
		Accepts: [MediaProvider.Imdb, MediaProvider.Tvdb],
		Yields: [MediaProvider.Tvmaze, MediaProvider.Tvdb, MediaProvider.Imdb],
		Cost: 2
	);

	public IReadOnlyList<IdLookup> Lookups { get; } = [ByShowId];

	public Task<Answer<IdLookupResult>> LookUpAsync(IdLookupRequest request, CancellationToken cancellationToken = default)
	{
		return LookUpResultAsync(request, cancellationToken).GuardedAsync(RemoteService.Tvmaze);
	}

	private async Task<Result<IdLookupResult>> LookUpResultAsync(IdLookupRequest request, CancellationToken cancellationToken)
	{
		return request switch
		{
			ByIdRequest(var lookup, var id) when lookup == ByShowId => await FindAsync(id, cancellationToken),
			_ => Error.From("TVmaze does not offer this lookup."),
		};
	}

	private Task<Result<IdLookupResult>> FindAsync(IExternalMediaId id, CancellationToken cancellationToken)
	{
		return tvmaze
			.LookUpAsync(id, cancellationToken)
			.Map(show => show.Match(found => new IdLookupResult(TvmazeFound.Ids(found), TvmazeFound.Hints(found)), () => new IdLookupResult([], new None())));
	}
}
