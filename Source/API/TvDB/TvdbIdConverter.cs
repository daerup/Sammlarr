using API.Plex;
using Contracts;
using Contracts.Media;
using Contracts.Media.Convertions;

namespace API.TvDB;

internal class TvdbIdConverter(PlexApi plexApi)
{
	public async Task<ConversionResult> ConvertAsync(
		IReadOnlyCollection<IMediaId> candidates,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	)
	{
		foreach (var id in candidates)
		{
			var result = await TryConvertAsync(id);
			if (result is Resolved)
			{
				return result;
			}
		}

		return new Unresolved();
	}

	private Task<ConversionResult> TryConvertAsync(IMediaId id) =>
		id switch
		{
			TvdbId tvdbId => Task.FromResult<ConversionResult>(new Resolved(tvdbId, [])),
			PlexId plexId => plexApi.ResolveById<TvdbId>(plexId),
			_ => Task.FromResult<ConversionResult>(new Unresolved()),
		};
}
