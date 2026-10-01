using System.Diagnostics;
using Contracts.Caching;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Providers;

namespace Core.Media;

internal sealed class MediaIdResolver(IKeyedServiceIndex<MediaProvider, IExternalIdConverter> converters, ITypedMemoryCache<string, bool> unmatchedCache)
{
	private static readonly TimeSpan unmatchedTtl = TimeSpan.FromHours(1);

	public async Task<Result<IReadOnlyList<IExternalMediaId>>> ResolveAsync(
		ResolveRequest request,
		MediaProvider target,
		CancellationToken cancellationToken = default
	)
	{
		var candidates = request.Candidates;
		var hints = request.Hints;
		var key = MediaUnmatchedCacheKey.Build(candidates, target);

		if (unmatchedCache.TryGetValue(key, out _))
		{
			return Error.From("Candidates were previously resolved as unmatched.");
		}

		Result<IReadOnlyList<IExternalMediaId>> discovered;
		try
		{
			discovered = await converters[target].ConvertAsync(candidates, hints, cancellationToken);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Activity.Current?.AddException(ex);
			return Error.From(ex);
		}

		return discovered
			.Map(found => (IReadOnlyList<IExternalMediaId>)[.. candidates.Concat(found).Distinct()])
			.ApplyError(_ => unmatchedCache.Set(key, true, unmatchedTtl));
	}
}
