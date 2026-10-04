using System.Globalization;
using API.Tmdb.Types;
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

	public async Task<Result<FoundMedia>> LookUpAsync(IExternalMediaId mediaId, CancellationToken cancellationToken = default)
	{
		var tmdbId = await TmdbIdOfAsync(mediaId, cancellationToken);
		return await tmdbId.Map(id => tmdbApi.GetDetailWithExternalIdsAsync(id, cancellationToken).Map(found => WithIds(found, id)));
	}

	private async Task<Result<TmdbId>> TmdbIdOfAsync(IExternalMediaId mediaId, CancellationToken cancellationToken)
	{
		return mediaId switch
		{
			TmdbId tmdbId => Success.Of(tmdbId),
			_ => await tmdbApi.FindAsync(mediaId, cancellationToken).Map(item => TmdbId.From(item.Id.ToString(CultureInfo.InvariantCulture), mediaId.Type)),
		};
	}

	private FoundMedia WithIds(TmdbTypes.Details found, TmdbId tmdbId)
	{
		var mapped = TmdbFound.Of(found, tmdbId, artwork);
		return mapped with { Externals = [.. mapped.Externals, .. found.ExternalIds?.Ids(tmdbId.Type) ?? []] };
	}
}
