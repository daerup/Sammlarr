namespace Contracts.Media;

public interface IMediaId
{
	MediaProvider Source { get; }
	MediaType Type { get; }
	string Value { get; }
}

public readonly record struct TmdbId : IMediaId
{
	public MediaProvider Source => MediaProvider.Tmdb;
	public string Value { get; private init; }
	public MediaType Type { get; private init; }

	public static TmdbId From(string id, MediaType type) => new() { Value = id, Type = type };
}

public readonly record struct PlexId : IMediaId
{
	public MediaProvider Source => MediaProvider.Plex;
	public string Value { get; private init; }
	public MediaType Type { get; private init; }

	public static PlexId From(string id, MediaType type) => new() { Value = id, Type = type };
}

public readonly record struct ImdbId : IMediaId
{
	public MediaProvider Source => MediaProvider.Imdb;
	public string Value { get; private init; }
	public MediaType Type { get; private init; }

	public static ImdbId From(string id, MediaType type) => new() { Value = id, Type = type };
}

public readonly record struct TvdbId : IMediaId
{
	public MediaProvider Source => MediaProvider.Tvdb;
	public string Value { get; private init; }
	public MediaType Type { get; private init; }

	public static TvdbId From(string id, MediaType type) => new() { Value = id, Type = type };
}
