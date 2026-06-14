using Contracts.Media;

namespace Integration.Tests.Infrastructure.Media;

public sealed class MediaCatalogContext
{
	private readonly List<CatalogEntry> _entries = [];
	private readonly Dictionary<(MediaProvider, string), CatalogEntry> _byId = new();
	private readonly Dictionary<string, CatalogEntry> _byAlias = new(StringComparer.Ordinal);

	public IEnumerable<CatalogEntry> Entries => _entries;

	public CatalogEntry Register(IReadOnlyDictionary<MediaProvider, string> ids, MediaType type, string? alias, int? year)
	{
		var entry = new CatalogEntry(ids, type, alias, year);
		_entries.Add(entry);
		foreach (var (provider, value) in ids)
			_byId[(provider, value)] = entry;
		if (!string.IsNullOrEmpty(alias))
			_byAlias[alias] = entry;
		return entry;
	}

	public CatalogEntry this[string handleOrToken] => Resolve(handleOrToken);

	public CatalogEntry Resolve(string handleOrToken)
	{
		if (handleOrToken.Contains(':'))
			return ResolveByIdToken(handleOrToken);
		if (_byAlias.TryGetValue(handleOrToken, out var entry))
			return entry;
		throw new InvalidOperationException($"No media declared as '{handleOrToken}'. Declare it with a 'media:' step first.");
	}

	public CatalogEntry ResolveByIdToken(string token)
	{
		if (_byId.TryGetValue(MediaProviders.ParseToken(token), out var entry))
			return entry;
		throw new InvalidOperationException($"No media declared with id '{token}'. Declare it with a 'media:' step first.");
	}

	public bool Contains(string handleOrToken) =>
		handleOrToken.Contains(':') ? _byId.ContainsKey(MediaProviders.ParseToken(handleOrToken)) : _byAlias.ContainsKey(handleOrToken);
}

public sealed record CatalogEntry(IReadOnlyDictionary<MediaProvider, string> Ids, MediaType MediaType, string? Alias, int? Year);
