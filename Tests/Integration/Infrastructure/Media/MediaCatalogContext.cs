using Contracts.Media;

namespace Integration.Tests.Infrastructure.Media;

public sealed class MediaCatalogContext
{
	private readonly Dictionary<string, CatalogEntry> _entries = new();

	public CatalogEntry this[string key] => _entries[key];

	public IEnumerable<CatalogEntry> Entries => _entries.Values;

	public bool Contains(string key) => _entries.ContainsKey(key);

	public CatalogEntry Register(string key, MediaType type, IReadOnlyDictionary<MediaProvider, string>? declaredIds = null, int? releaseYear = null)
	{
		var entry = new CatalogEntry(key, key, type, releaseYear, declaredIds);
		_entries[key] = entry;
		return entry;
	}
}

public sealed record CatalogEntry(
	string Key,
	string Title,
	MediaType MediaType,
	int? ReleaseYear,
	IReadOnlyDictionary<MediaProvider, string>? DeclaredIds = null
);
