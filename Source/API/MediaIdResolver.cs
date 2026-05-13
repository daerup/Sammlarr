using API.IMDB;
using API.Plex;
using API.TMDB;
using API.TvDB;
using Contracts;
using Contracts.Media;
using Contracts.Media.Convertions;
using Shared.Telemetry;

namespace API;

internal class MediaIdResolver(
	IMediaIdGroupRepository mediaIdGroupRepository,
	TmdbIdConverter tmdb,
	PlexIdConverter plex,
	ImdbIdConverter imdb,
	TvdbIdConverter tvdb
) : IMediaIdResolver
{
	public async Task<ConversionResult> ResolveAsync(IReadOnlyCollection<IMediaId> candidates, MediaProvider target, Optional<ResolutionHints> hints)
	{
		var title = hints is Some<ResolutionHints> some ? some.Value.Title : null;
		using var activity = SammlarrTelemetry.StartIdResolution(target, candidates, title);
		var cachedIds = await mediaIdGroupRepository.GetByIdsAsync(candidates);
		var cachedTarget = cachedIds.FirstOrDefault(id => id.Source == target);
		if (cachedTarget is not null)
		{
			SammlarrTelemetry.TagIdResolutionOutcome(activity, IdResolutionOutcome.Hit, target);
			return new Resolved(cachedTarget, cachedIds.Except([cachedTarget]));
		}

		ConversionResult conversionResult = target switch
		{
			MediaProvider.Plex => await plex.ConvertAsync(candidates, hints),
			MediaProvider.Tmdb => await tmdb.ConvertAsync(candidates, hints),
			MediaProvider.Imdb => await imdb.ConvertAsync(candidates, hints),
			MediaProvider.Tvdb => await tvdb.ConvertAsync(candidates, hints),
			MediaProvider.Mdblist => new Unresolved(),
		};

		if (conversionResult is Resolved resolved)
		{
			await mediaIdGroupRepository.UpsertAsync(resolved.AllIds);
			SammlarrTelemetry.TagIdResolutionOutcome(activity, IdResolutionOutcome.Resolved, target);
			return resolved;
		}

		SammlarrTelemetry.TagIdResolutionOutcome(activity, IdResolutionOutcome.Unresolved, target);
		return conversionResult;
	}
}
