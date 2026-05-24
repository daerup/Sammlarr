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
	IMediaRepository mediaRepository,
	TmdbIdConverter tmdb,
	PlexIdConverter plex,
	ImdbIdConverter imdb,
	TvdbIdConverter tvdb,
	ITypedMemoryCache<string, bool> unresolvedCache
) : IMediaIdResolver
{
	private static readonly TimeSpan UnresolvedTtl = TimeSpan.FromHours(1);

	public async Task<ConversionResult> ResolveAsync(
		IEnumerable<IExternalMediaId> candidates,
		MediaProvider target,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	)
	{
		var materialized = candidates.ToList();
		var title = hints is Some<ResolutionHints> some ? some.Value.Title : null;
		using var activity = SammlarrTelemetry.StartIdResolution(target, materialized, title);
        var cached = await mediaRepository.FindAsync(materialized, cancellationToken);
        if (cached.HasValue && cached.Value.Externals.Any(e => e.Source == target))
		{
			var merged = await MergeCandidatesAsync(cached.Value, materialized, cancellationToken);
			SammlarrTelemetry.TagIdResolutionOutcome(activity, IdResolutionOutcome.Hit, target);
			return new Resolved(merged.Id, merged.Externals, merged.Externals.First(e => e.Source == target));
		}

		var unresolvedKey = BuildUnresolvedKey(materialized, target);
		if (unresolvedCache.TryGetValue(unresolvedKey, out _))
		{
			SammlarrTelemetry.TagIdResolutionOutcome(activity, IdResolutionOutcome.Unresolved, target);
			return new Unresolved();
		}

		Optional<IReadOnlyList<IExternalMediaId>> discovered = new None();
		try
		{
			discovered = target switch
			{
				MediaProvider.Plex => await plex.ConvertAsync(materialized, hints, cancellationToken),
				MediaProvider.Tmdb => await tmdb.ConvertAsync(materialized, hints, cancellationToken),
				MediaProvider.Imdb => await imdb.ConvertAsync(materialized, hints, cancellationToken),
				MediaProvider.Tvdb => await tvdb.ConvertAsync(materialized, hints, cancellationToken),
				MediaProvider.Mdblist => new None(),
			};
		}
		catch (Exception ex)
		{
			activity?.AddException(ex);
		}

		if (discovered is Some<IReadOnlyList<IExternalMediaId>>(var found))
		{
			var combined = materialized.Concat(found).ToHashSet();
			var identity = await mediaRepository.UpsertAsync(combined, cancellationToken);
			SammlarrTelemetry.TagIdResolutionOutcome(activity, IdResolutionOutcome.Resolved, target);
			return new Resolved(identity.Id, identity.Externals, identity.Externals.First(e => e.Source == target));
		}

		unresolvedCache.Set(unresolvedKey, true, UnresolvedTtl);
		SammlarrTelemetry.TagIdResolutionOutcome(activity, IdResolutionOutcome.Unresolved, target);
		return new Unresolved();
	}

	private async Task<MediaIdentity> MergeCandidatesAsync(MediaIdentity cached, List<IExternalMediaId> candidates, CancellationToken cancellationToken)
	{
		var missing = candidates.Where(c => !cached.Externals.Contains(c)).ToList();
		if (missing.Count == 0)
		{
			return cached;
		}

		var combined = cached.Externals.Concat(missing).ToList();
		return await mediaRepository.UpsertAsync(combined, cancellationToken);
	}

	private static string BuildUnresolvedKey(List<IExternalMediaId> candidates, MediaProvider target)
	{
		var sortedIds = candidates.Select(id => id.ToString()).OrderBy(id => id, StringComparer.Ordinal);
		return $"{target}|{string.Join(",", sortedIds)}";
	}
}
