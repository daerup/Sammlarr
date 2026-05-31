using Contracts.Media;

namespace Integration.Tests.Infrastructure.Media;

internal static class MediaVersionResolver
{
	public static ResolvedVersion Resolve(CatalogEntry entry, MediaRow overlay)
	{
		var ids = new Dictionary<MediaProvider, string>(entry.DeclaredIds ?? MediaIdDefaults.ForOrdinal(entry.Ordinal));
		if (overlay.Ids is not null)
			foreach (var (provider, id) in overlay.Ids)
				ids[provider] = id;

		return new ResolvedVersion(
			ids,
			entry.MediaType,
			overlay.Year ?? entry.ReleaseYear ?? MediaIdDefaults.DefaultYear,
			Cells.Text(overlay.Title) ?? entry.Title,
			Cells.Text(overlay.Poster),
			Cells.Text(overlay.Summary),
			overlay.Genres
		);
	}
}
