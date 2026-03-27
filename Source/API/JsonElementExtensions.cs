using System.Text.Json;

namespace API;

internal static class JsonElementExtensions
{
	extension(JsonElement element)
	{
		public DateTimeOffset ReadUnixTime(string propertyName)
		{
			if (element.TryGetProperty(propertyName, out var candidate) && candidate.ValueKind == JsonValueKind.Number && candidate.TryGetInt64(out var unix))
			{
				return DateTimeOffset.FromUnixTimeSeconds(unix);
			}

			return DateTimeOffset.MinValue;
		}

		public string ReadString(params string[] propertyNames)
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

		public DateTimeOffset ReadDate(string propertyName, string fallbackYearPropertyName)
		{
			var value = element.ReadString(propertyName);
			if (DateTimeOffset.TryParse(value, out var parsed))
			{
				return parsed;
			}

			var parsedYear = int.Parse(element.ReadString(fallbackYearPropertyName));
			return new DateTime(parsedYear, 1, 1);
		}
	}
}
