using System.Diagnostics;
using Contracts;
using Contracts.Caching;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Convertions;

namespace Core.Media;

internal sealed class MediaIdResolver(IKeyedServiceIndex<MediaProvider, IExternalIdConverter> converters, ITypedMemoryCache<string, bool> unresolvedCache)
{
	private static readonly TimeSpan unresolvedTtl = TimeSpan.FromHours(1);

	public async Task<Optional<IReadOnlyList<IExternalMediaId>>> ResolveAsync(ResolveRequest request, CancellationToken cancellationToken = default)
	{
		var candidates = request.Candidates;
		var target = request.Target;
		var hints = request.Hints;
		var key = MediaUnresolvedCacheKey.Build(candidates, target);

		if (unresolvedCache.TryGetValue(key, out _))
		{
			return new None();
		}

		Optional<IReadOnlyList<IExternalMediaId>> discovered;
		try
		{
			discovered = await converters[target].ConvertAsync(candidates, hints, cancellationToken);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Activity.Current?.AddException(ex);
			return new None();
		}

		if (discovered is Some<IReadOnlyList<IExternalMediaId>>(var found))
		{
			return Some.Of<IReadOnlyList<IExternalMediaId>>([.. candidates.Concat(found).Distinct()]);
		}

		unresolvedCache.Set(key, true, unresolvedTtl);
		return new None();
	}
}
