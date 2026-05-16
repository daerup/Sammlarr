using API.IMDB;
using API.Plex;
using API.TMDB;
using API.TvDB;
using Contracts;
using Contracts.Caching;
using Contracts.Media;
using Contracts.Media.Convertions;
using Shared.Telemetry;

namespace API;

internal class MediaIdResolver(
	IMediaIdGroupRepository mediaIdGroupRepository,
	TmdbIdConverter tmdb,
	PlexIdConverter plex,
	ImdbIdConverter imdb,
	TvdbIdConverter tvdb,
	ITypedMemoryCache<string, bool> unresolvedCache
) : IMediaIdResolver
{
	private static readonly TimeSpan UnresolvedTtl = TimeSpan.FromHours(1);

	public async Task<ConversionResult> ResolveAsync(
		IReadOnlyCollection<IMediaId> candidates,
		MediaProvider target,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	)
	{
		var title = hints is Some<ResolutionHints> some ? some.Value.Title : null;
		using var activity = SammlarrTelemetry.StartIdResolution(target, candidates, title);
		var cachedIds = await mediaIdGroupRepository.GetByIdsAsync(candidates, cancellationToken);
		var cachedTarget = cachedIds.FirstOrDefault(id => id.Source == target);
		if (cachedTarget is not null)
		{
			await MergeNewCandidatesAsync(cachedIds, candidates, cancellationToken);
			SammlarrTelemetry.TagIdResolutionOutcome(activity, IdResolutionOutcome.Hit, target);
			return new Resolved(cachedTarget, cachedIds.Except([cachedTarget]));
		}

		var unresolvedKey = BuildUnresolvedKey(candidates, target);
		if (unresolvedCache.TryGetValue(unresolvedKey, out _))
		{
			SammlarrTelemetry.TagIdResolutionOutcome(activity, IdResolutionOutcome.Unresolved, target);
			return new Unresolved();
		}

		ConversionResult conversionResult = new Unresolved();
		try
		{
			conversionResult = target switch
			{
				MediaProvider.Plex => await plex.ConvertAsync(candidates, hints, cancellationToken),
				MediaProvider.Tmdb => await tmdb.ConvertAsync(candidates, hints, cancellationToken),
				MediaProvider.Imdb => await imdb.ConvertAsync(candidates, hints, cancellationToken),
				MediaProvider.Tvdb => await tvdb.ConvertAsync(candidates, hints, cancellationToken),
				MediaProvider.Mdblist => new Unresolved(),
			};
		}
		catch (Exception ex)
		{
			activity?.AddException(ex);
		}

		if (conversionResult is Resolved resolved)
		{
			await mediaIdGroupRepository.UpsertAsync(resolved.AllIds.Concat(candidates), cancellationToken);
			SammlarrTelemetry.TagIdResolutionOutcome(activity, IdResolutionOutcome.Resolved, target);
			return resolved;
		}

		unresolvedCache.Set(unresolvedKey, true, UnresolvedTtl);
		SammlarrTelemetry.TagIdResolutionOutcome(activity, IdResolutionOutcome.Unresolved, target);
		return conversionResult;
	}

	private async ValueTask MergeNewCandidatesAsync(List<IMediaId> cachedIds, IEnumerable<IMediaId> candidates, CancellationToken cancellationToken)
	{
		var missing = candidates.Except(cachedIds).ToList();
		if (missing.Count == 0)
		{
			return;
		}

		await mediaIdGroupRepository.UpsertAsync(cachedIds.Concat(missing), cancellationToken);
		cachedIds.AddRange(missing);
	}

	private static string BuildUnresolvedKey(IEnumerable<IMediaId> candidates, MediaProvider target)
	{
		var sortedIds = candidates.Select(id => id.ToString()).OrderBy(id => id, StringComparer.Ordinal);
		return $"{target}|{string.Join(",", sortedIds)}";
	}
}
