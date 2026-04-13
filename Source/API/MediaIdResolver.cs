using API.IMDB;
using API.Plex;
using API.TMDB;
using API.TvDB;
using Contracts;
using Contracts.Media;
using Contracts.Media.Convertions;

namespace API;

internal class MediaIdResolver(
	IMediaIdGroupRepository mediaIdGroupRepository,
	TmdbIdConverter tmdb,
	PlexIdConverter plex,
	ImdbIdConverter imdb,
	TvdbIdConverter tvdb
) : IMediaIdResolver
{
	public async Task<ConversionResult> ResolveAsync(IReadOnlyCollection<IMediaId> candidates, MediaProvider target, Option<ResolutionHints> hints)
	{
		foreach (var candidate in candidates)
		{
			var cachedIds = await mediaIdGroupRepository.GetByIdAsync(candidate);
			var cachedTarget = cachedIds.FirstOrDefault(id => id.Source == target);
			if (cachedTarget is not null)
				return new Resolved(cachedTarget, cachedIds.Except([cachedTarget]));
		}

		ConversionResult conversionResult = target switch
		{
			MediaProvider.Plex => await plex.ConvertAsync(candidates, hints),
			MediaProvider.Tmdb => await tmdb.ConvertAsync(candidates, hints),
			MediaProvider.Imdb => await imdb.ConvertAsync(candidates, hints),
			MediaProvider.Tvdb => await tvdb.ConvertAsync(candidates, hints),
			MediaProvider.Mdblist => new Unresolved(), // Mdblist is a list source, not a resolution target
		};

		return conversionResult switch
		{
			Resolved resolved => await PersistAndReturnAsync(resolved),
			Unresolved unresolved => unresolved,
		};
	}

	private async Task<Resolved> PersistAndReturnAsync(Resolved resolved)
	{
		await mediaIdGroupRepository.CreateOrAppendAsync(resolved.AllIds);
		return resolved;
	}
}
