using Contracts.Media;

namespace Integration.Tests.Infrastructure.Media;

public sealed class MediaCatalogContext
{
	private int _nextOrdinal = 1;
	private readonly Dictionary<string, CatalogEntry> _entries = new();

	public CatalogEntry this[string key] => _entries[key];

	public IEnumerable<CatalogEntry> Entries => _entries.Values;

	public bool Contains(string key) => _entries.ContainsKey(key);

	public CatalogEntry Register(string key, MediaType type, IReadOnlyDictionary<MediaProvider, string>? declaredIds = null, int? releaseYear = null)
	{
		var entry = new CatalogEntry(key, key, type, releaseYear, _nextOrdinal++, declaredIds);
		_entries[key] = entry;
		return entry;
	}

	public CatalogEntry GetOrRegister(string key, MediaType type) => _entries.TryGetValue(key, out var existing) ? existing : Register(key, type);
}

public sealed record CatalogEntry(
	string Key,
	string Title,
	MediaType MediaType,
	int? ReleaseYear,
	int Ordinal,
	IReadOnlyDictionary<MediaProvider, string>? DeclaredIds = null
);
