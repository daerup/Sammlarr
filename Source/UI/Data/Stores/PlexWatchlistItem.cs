using System;
using System.Text.Json;

namespace UI.Data.Stores;

public sealed class PlexWatchlistItem
{
	public string RatingKey { get; init; } = string.Empty;

	public string? Title { get; init; }

	public string? Summary { get; init; }

	public string? Type { get; init; }

	public DateTimeOffset? AddedAt { get; init; }

	public static PlexWatchlistItem FromJson(JsonElement element)
	{
		var addedAt = default(DateTimeOffset?);
		if (TryGetUnixTimestamp(element, "added_at", out var unix))
		{
			addedAt = DateTimeOffset.FromUnixTimeSeconds(unix);
		}
		else if (TryGetUnixTimestamp(element, "addedAt", out unix))
		{
			addedAt = DateTimeOffset.FromUnixTimeSeconds(unix);
		}

		return new PlexWatchlistItem
		{
			RatingKey = ReadString(element, "rating_key", "ratingKey"),
			Title = TryGetString(element, "title"),
			Summary = TryGetString(element, "summary"),
			Type = TryGetString(element, "type"),
			AddedAt = addedAt,
		};
	}

	private static bool TryGetUnixTimestamp(JsonElement element, string propertyName, out long value)
	{
		value = 0;
		if (element.TryGetProperty(propertyName, out var candidate) && candidate.ValueKind == JsonValueKind.Number)
		{
			return candidate.TryGetInt64(out value);
		}

		return false;
	}

	private static string ReadString(JsonElement element, params string[] propertyNames)
	{
		foreach (var name in propertyNames)
		{
			if (element.TryGetProperty(name, out var candidate) && candidate.ValueKind == JsonValueKind.String)
			{
				return candidate.GetString() ?? string.Empty;
			}
		}

		return string.Empty;
	}

	private static string? TryGetString(JsonElement element, string propertyName) =>
		element.TryGetProperty(propertyName, out var candidate) && candidate.ValueKind == JsonValueKind.String ? candidate.GetString() : null;
}
