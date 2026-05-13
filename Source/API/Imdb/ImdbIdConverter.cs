using API.Plex;
using Contracts;
using Contracts.Media;
using Contracts.Media.Convertions;

namespace API.IMDB;

internal class ImdbIdConverter(PlexApi plexApi)
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
			ImdbId imdbId => Task.FromResult<ConversionResult>(new Resolved(imdbId, [])),
			PlexId plexId => ResolveFromPlexAsync(plexId),
			_ => Task.FromResult<ConversionResult>(new Unresolved()),
		};

	private Task<ConversionResult> ResolveFromPlexAsync(PlexId plexId) => plexApi.ResolveById<ImdbId>(plexId);
}
