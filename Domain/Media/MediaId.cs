using Domain.Providers;
using Vogen;

namespace Domain.Media;

public readonly record struct MediaIdentity(MediaId Id, ValueSet<IExternalMediaId> Externals)
{
	public MediaId Id { get; init; } =
		Id.IsInitialized() ? Id : throw new ArgumentException("An identity needs a MediaId; a provider's answer is FoundMedia.", nameof(Id));

	public MediaType Type => Externals.FirstOrDefault()?.Type ?? MediaType.Unknown;
}

[ValueObject<Guid>]
public readonly partial struct MediaId
{
	private static Validation Validate(Guid input) => input == Guid.Empty ? Validation.Invalid("MediaId cannot be empty.") : Validation.Ok;

	public static MediaId New() => From(Guid.NewGuid());

	public override string ToString() => IsInitialized() ? Value.ToString("N") : "[UNINITIALIZED]";
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

public static class ExternalMediaId
{
	public static Result<T> Require<T>(this IExternalMediaId id, string reason)
		where T : struct, IExternalMediaId => id is T typed ? Success.Of(typed) : Error.From(reason);
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

public readonly record struct RottenTomatoesId : IExternalMediaId
{
	public MediaProvider Source => MediaProvider.RottenTomatoes;
	public string Value { get; private init; }
	public MediaType Type { get; private init; }

	public static RottenTomatoesId From(string path, MediaType type) => new() { Value = path, Type = type };

	public override string ToString() => ExternalMediaIdExtensions.ToString(this);
}
