using API.Plex;
using Contracts;
using Contracts.Media;
using Contracts.Media.Convertions;

namespace API.TvDB;

internal class TvdbIdConverter(PlexApi plexApi)
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
			TvdbId tvdbId => Task.FromResult<ConversionResult>(new Resolved(tvdbId, [])),
			PlexId plexId => ResolveFromPlexAsync(plexId),
			_ => Task.FromResult<ConversionResult>(new Unresolved()),
		};

	private Task<ConversionResult> ResolveFromPlexAsync(PlexId plexId) => plexApi.ResolveById<TvdbId>(plexId);
}
