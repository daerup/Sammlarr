using Contracts.Media;
using Domain.Media;
using Domain.Providers;

namespace Integration.Tests.Infrastructure.Media;

internal static class MediaVersionResolver
{
	public static ResolvedVersion Build(
		CatalogEntry entry,
		string? title = null,
		string? summary = null,
		IReadOnlyList<string>? genres = null,
		string? poster = null,
		int? year = null
	) => Make(entry, entry.Ids, title, summary, genres, poster, year);

	public static ResolvedVersion BuildSubset(
		CatalogEntry entry,
		IReadOnlyList<MediaProvider> providers,
		string? title = null,
		string? summary = null,
		IReadOnlyList<string>? genres = null,
		string? poster = null,
		int? year = null
	)
	{
		var ids = new Dictionary<MediaProvider, string>();
		foreach (var provider in providers)
		{
			if (!entry.Ids.TryGetValue(provider, out var id))
				throw new InvalidOperationException($"Media '{entry.Alias ?? "?"}' has no {provider} id to satisfy this step.");
			ids[provider] = id;
		}
		return Make(entry, ids, title, summary, genres, poster, year);
	}

	private static ResolvedVersion Make(
		CatalogEntry entry,
		IReadOnlyDictionary<MediaProvider, string> ids,
		string? title,
		string? summary,
		IReadOnlyList<string>? genres,
		string? poster,
		int? year
	) => new(ids, entry.MediaType, year ?? entry.Year ?? MediaIdDefaults.DefaultYear, title ?? entry.Alias ?? string.Empty, poster, summary, genres);
}
