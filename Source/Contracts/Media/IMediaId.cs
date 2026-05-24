namespace Contracts.Media;

public readonly record struct MediaIdentity(MediaId Id, IReadOnlyList<IExternalMediaId> Externals);

public readonly record struct MediaId
{
	public Guid Value { get; private init; }

	public static MediaId From(Guid value)
	{
		if (value == Guid.Empty)
		{
			throw new ArgumentException("MediaId cannot be empty.", nameof(value));
		}
		return new() { Value = value };
	}

	public override string ToString() => Value.ToString("N");
}

public interface IExternalMediaId
{
	MediaProvider Source { get; }
	MediaType Type { get; }
	string Value { get; }
}

file static class ExternalMediaIdExtensions
{
	public static string ToString(this IExternalMediaId id) => $"{id.Source}:{id.Type}:{id.Value}";
}

public readonly record struct TmdbId : IExternalMediaId
{
	public MediaProvider Source => MediaProvider.Tmdb;
	public string Value { get; private init; }
	public MediaType Type { get; private init; }

	public static TmdbId From(string id, MediaType type) => new() { Value = id, Type = type };

	public override string ToString() => ExternalMediaIdExtensions.ToString(this);
}

public readonly record struct PlexId : IExternalMediaId
{
	public MediaProvider Source => MediaProvider.Plex;
	public string Value { get; private init; }
	public MediaType Type { get; private init; }

	public static PlexId From(string id, MediaType type) => new() { Value = id, Type = type };

	public override string ToString() => ExternalMediaIdExtensions.ToString(this);
}

public readonly record struct ImdbId : IExternalMediaId
{
	public MediaProvider Source => MediaProvider.Imdb;
	public string Value { get; private init; }
	public MediaType Type { get; private init; }

	public static ImdbId From(string id, MediaType type) => new() { Value = id, Type = type };

	public override string ToString() => ExternalMediaIdExtensions.ToString(this);
}

public readonly record struct TvdbId : IExternalMediaId
{
	public MediaProvider Source => MediaProvider.Tvdb;
	public string Value { get; private init; }
	public MediaType Type { get; private init; }

	public static TvdbId From(string id, MediaType type) => new() { Value = id, Type = type };

	public override string ToString() => ExternalMediaIdExtensions.ToString(this);
}

public readonly record struct MdblistId : IExternalMediaId
{
	public MediaProvider Source => MediaProvider.Mdblist;
	public string Value { get; private init; }
	public MediaType Type { get; private init; }

	public static MdblistId From(string id, MediaType type) => new() { Value = id, Type = type };

	public override string ToString() => ExternalMediaIdExtensions.ToString(this);
}
