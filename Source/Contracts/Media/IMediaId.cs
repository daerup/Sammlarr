using Vogen;

namespace Contracts.Media;

public interface IMediaId
{
	MediaProvider Source { get; }
}

[ValueObject<int>]
public readonly partial record struct TmdbId : IMediaId
{
	public MediaProvider Source => MediaProvider.Tmdb;
}

[ValueObject<string>]
public readonly partial record struct PlexId : IMediaId
{
	public MediaProvider Source => MediaProvider.Plex;
}

[ValueObject<string>]
public readonly partial record struct ImdbId : IMediaId
{
	public MediaProvider Source => MediaProvider.Imdb;
}

[ValueObject<int>]
public readonly partial record struct TvdbId : IMediaId
{
	public MediaProvider Source => MediaProvider.Tvdb;
}
