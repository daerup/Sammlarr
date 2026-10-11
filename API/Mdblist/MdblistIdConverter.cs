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

	public Task<Answer<IdLookupResult>> LookUpAsync(IdLookupRequest request, CancellationToken cancellationToken = default)
	{
		return request switch
		{
			ByIdRequest(var lookup, var id) when lookup == ByAnyId => FindAsync(id, cancellationToken),
			_ => Task.FromResult<Answer<IdLookupResult>>(new NoMatch(RemoteService.Mdblist)),
		};
	}

	private async Task<Answer<IdLookupResult>> FindAsync(IExternalMediaId id, CancellationToken cancellationToken)
	{
		var answer = await titles.FindAsync([id], cancellationToken);
		return answer is NoMatch ? new Found<IdLookupResult>(new IdLookupResult([], new None())) : answer.Map(title => MdblistIds.Found(title, id.Type));
	}
}
