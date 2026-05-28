using Contracts.Media;

namespace Integration.Tests.Infrastructure.Media;

public sealed class MediaCatalogContext
{
	private int _nextId = 1;
	private readonly Dictionary<string, RegisteredMedia> _entries = new();

	public RegisteredMedia this[string key] => _entries[key];

	public void RegisterMovie(string title) => Add(title, "movie");

	public void RegisterPerson(string name) => Add(name, "person");

	private void Add(string key, string tmdbMediaType)
	{
		_entries[key] = new RegisteredMedia(_nextId++, key, tmdbMediaType);
	}
}

public sealed record RegisteredMedia(int TmdbId, string Title, string TmdbMediaType)
{
	public MediaType MediaType =>
		TmdbMediaType switch
		{
			"movie" => MediaType.Movie,
			"tv" => MediaType.Show,
			_ => MediaType.Unknown,
		};
}
