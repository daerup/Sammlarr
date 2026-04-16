using API.Plex;
using Contracts.Media;
using Contracts.Media.Convertions;

namespace API.TMDB;

internal class TmdbIdConverter(PlexApi plexApi)
{
	public async Task<ConversionResult> ConvertAsync(IReadOnlyCollection<IMediaId> candidates, Option<ResolutionHints> hints)
	{
		foreach (var id in candidates)
		{
			var result = await TryConvertAsync(id);
			if (result is Resolved)
				return result;
		}

		return new Unresolved();
	}

	private Task<ConversionResult> TryConvertAsync(IMediaId id) =>
		id switch
		{
			TmdbId tmdbId => Task.FromResult<ConversionResult>(new Resolved(tmdbId, [])),
			PlexId plexId => ResolveFromPlexAsync(plexId),
			_ => Task.FromResult<ConversionResult>(new Unresolved()),
		};

	private Task<ConversionResult> ResolveFromPlexAsync(PlexId plexId) => plexApi.ResolveById<TmdbId>(plexId);
}
