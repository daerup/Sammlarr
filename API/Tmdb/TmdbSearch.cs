using Contracts.Media.Search;
using Domain.Media;

namespace API.Tmdb;

internal sealed class TmdbSearch(TmdbApi tmdbApi, TmdbArtwork artwork) : IMediaSearchProvider
{
	public async Task<IReadOnlyList<FoundMedia>> SearchAsync(
		string query,
		IReadOnlyCollection<MediaType> mediaTypes,
		CancellationToken cancellationToken = default
	)
	{
		var results = await tmdbApi.SearchAsync(query, cancellationToken);
		return
		[
			.. results
				.GetOrThrow(error => new InvalidOperationException(error.Reason))
				.Where(item => mediaTypes.Contains(item.MediaType))
				.Select(item => TmdbFound.Of(item, artwork)),
		];
	}
}
