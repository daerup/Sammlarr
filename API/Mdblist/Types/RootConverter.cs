using System.Text.Json;
using System.Text.Json.Serialization;
using Contracts.Media;
using Domain.Media;

namespace API.Mdblist.Types;

internal sealed class RootConverter : JsonConverter<MdblistTypes.ListItems.Root>
{
	private static readonly MdblistTypes.ListItems.Ids EmptyIds = new(null, null, null, null, null);

	public override MdblistTypes.ListItems.Root Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		using var doc = JsonDocument.ParseValue(ref reader);
		var (movies, movieUnknowns) = SplitSection<MdblistTypes.ListItems.Movie>(doc.RootElement, "movies", MediaType.Movie, options);
		var (shows, showUnknowns) = SplitSection<MdblistTypes.ListItems.Show>(doc.RootElement, "shows", MediaType.Show, options);
		return new MdblistTypes.ListItems.Root(movies, shows, [.. movieUnknowns, .. showUnknowns]);
	}

	public override void Write(Utf8JsonWriter writer, MdblistTypes.ListItems.Root value, JsonSerializerOptions options) => throw new NotSupportedException();

	private static (IReadOnlyList<T> Strict, IReadOnlyList<MdblistTypes.ListItems.UnknownListItem> Unknown) SplitSection<T>(
		JsonElement root,
		string propertyName,
		MediaType mediaType,
		JsonSerializerOptions options
	)
	{
		if (!root.TryGetProperty(propertyName, out var section) || section.ValueKind != JsonValueKind.Array)
		{
			return ([], []);
		}
		var strict = new List<T>(section.GetArrayLength());
		var unknown = new List<MdblistTypes.ListItems.UnknownListItem>();
		foreach (var element in section.EnumerateArray())
		{
			if (TryStrict<T>(element, options) is { } parsed)
			{
				strict.Add(parsed);
			}
			else
			{
				unknown.Add(BuildUnknown(element, mediaType, options));
			}
		}
		return (strict, unknown);
	}

	private static T? TryStrict<T>(JsonElement element, JsonSerializerOptions options)
	{
		try
		{
			var parsed = element.Deserialize<T>(options);
			if (parsed is MdblistTypes.ListItems.IListItem { ids: not null, title.Length: > 0 })
			{
				return parsed;
			}
			return default;
		}
		catch (JsonException)
		{
			return default;
		}
	}

	private static MdblistTypes.ListItems.UnknownListItem BuildUnknown(JsonElement element, MediaType mediaType, JsonSerializerOptions options)
	{
		var ids = element.TryGetProperty("ids", out var idsElement) ? TryDeserializeIds(idsElement, options) ?? EmptyIds : EmptyIds;
		var title = element.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? string.Empty : string.Empty;
		var year = element.TryGetProperty("release_year", out var y) && y.ValueKind == JsonValueKind.Number && y.TryGetInt32(out var yr) ? yr : 0;
		return new MdblistTypes.ListItems.UnknownListItem(mediaType, ids, title, year);
	}

	private static MdblistTypes.ListItems.Ids? TryDeserializeIds(JsonElement element, JsonSerializerOptions options)
	{
		try
		{
			return element.Deserialize<MdblistTypes.ListItems.Ids>(options);
		}
		catch (JsonException)
		{
			return null;
		}
	}
}
