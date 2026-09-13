using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Tests.Recording;

internal sealed record RecordedResponse(string Provider, string Slug, string Path, string Body);

internal static class RecordedStubs
{
	private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

	private static readonly string[] CredentialParams = ["api_key", "apikey", "x-plex-token", "token"];

	private sealed record RecordedEntry(
		ProviderEndpoint Provider,
		string Method,
		string ReplayPath,
		SortedDictionary<string, string> Query,
		int Status,
		string ContentType,
		string Body
	)
	{
		internal string Route => $"{Provider.Name}{ReplayPath}";

		internal string Discriminator => string.Join('-', Query.Values.Select(Sanitize));
	}

	internal static IReadOnlyList<RecordedResponse> Write(string snapshotJson, string mappingsDirectory, Func<string, bool> isStubbed)
	{
		var entries = Parse(snapshotJson).Where(entry => !isStubbed(entry.ReplayPath)).ToList();
		var ambiguous = entries
			.GroupBy(entry => entry.Route, StringComparer.Ordinal)
			.Where(group => group.Select(entry => entry.Discriminator).Distinct(StringComparer.Ordinal).Count() > 1)
			.Select(group => group.Key)
			.ToHashSet(StringComparer.Ordinal);

		var recorded = new List<RecordedResponse>();
		foreach (var entry in entries)
		{
			var slug = Slug(entry, ambiguous.Contains(entry.Route));
			Directory.CreateDirectory(Path.Combine(mappingsDirectory, entry.Provider.Name));
			File.WriteAllText(Path.Combine(mappingsDirectory, entry.Provider.Name, $"{slug}.json"), Mapping(entry, slug).ToJsonString(Pretty));
			recorded.Add(new RecordedResponse(entry.Provider.Name, slug, entry.ReplayPath, entry.Body));
		}

		return recorded;
	}

	private static List<RecordedEntry> Parse(string snapshotJson)
	{
		var entries = new List<RecordedEntry>();
		foreach (var mapping in JsonNode.Parse(snapshotJson)?["mappings"]?.AsArray() ?? [])
		{
			var request = mapping?["request"];
			var url = request?["url"]?.GetValue<string>() ?? request?["urlPath"]?.GetValue<string>();
			if (url is null)
			{
				continue;
			}

			var provider = ProviderRecording
				.AllEndpoints.OrderByDescending(candidate => candidate.Prefix.Length)
				.FirstOrDefault(candidate => url.StartsWith($"{candidate.Prefix}/", StringComparison.Ordinal));
			if (provider is null)
			{
				continue;
			}

			var replayPath = url.Split('?')[0][provider.Prefix.Length..];
			var response = mapping!["response"]!;
			entries.Add(
				new RecordedEntry(
					provider,
					request!["method"]?.GetValue<string>() ?? "GET",
					replayPath.Length == 0 ? "/" : replayPath,
					QueryMatchers(request, url),
					response["status"]?.GetValue<int>() ?? 200,
					ContentType(response["headers"]),
					Body(response)
				)
			);
		}

		return entries;
	}

	private static JsonObject Mapping(RecordedEntry entry, string slug)
	{
		var request = new JsonObject { ["method"] = entry.Method, ["urlPath"] = entry.ReplayPath };
		if (entry.Query.Count > 0)
		{
			request["queryParameters"] = Matchers(entry.Query);
		}

		return new JsonObject
		{
			["request"] = request,
			["response"] = new JsonObject
			{
				["status"] = entry.Status,
				["headers"] = new JsonObject { ["Content-Type"] = entry.ContentType },
				["bodyFileName"] = $"{entry.Provider.Name}/{slug}.verified.json",
			},
		};
	}

	private static SortedDictionary<string, string> QueryMatchers(JsonNode? request, string url)
	{
		var matchers = new SortedDictionary<string, string>(StringComparer.Ordinal);
		foreach (var pair in url.Split('?').Skip(1).SelectMany(query => query.Split('&')))
		{
			var parts = pair.Split('=', 2);
			if (parts.Length == 2 && !CredentialParams.Contains(parts[0], StringComparer.OrdinalIgnoreCase))
			{
				matchers[parts[0]] = Uri.UnescapeDataString(parts[1]);
			}
		}

		if (request?["queryParameters"] is not JsonObject parameters)
		{
			return matchers;
		}

		foreach (var (key, value) in parameters)
		{
			if (CredentialParams.Contains(key, StringComparer.OrdinalIgnoreCase))
			{
				continue;
			}

			if (EqualTo(value) is { } equalTo)
			{
				matchers[key] = equalTo;
			}
		}

		return matchers;
	}

	private static string? EqualTo(JsonNode? matcher) =>
		matcher?["equalTo"]?.GetValue<string>() ?? (matcher?["hasExactly"] as JsonArray)?.FirstOrDefault()?["equalTo"]?.GetValue<string>();

	private static JsonObject Matchers(SortedDictionary<string, string> query)
	{
		var node = new JsonObject();
		foreach (var (key, value) in query)
		{
			node[key] = new JsonObject { ["equalTo"] = value };
		}

		return node;
	}

	private static string Slug(RecordedEntry entry, bool disambiguate)
	{
		var basePart = entry.ReplayPath.Trim('/').Replace('/', '-') is { Length: > 0 } path ? path : "root";
		return disambiguate && entry.Query.Count > 0 ? $"{basePart}-{entry.Discriminator}" : basePart;
	}

	private static string Sanitize(string value) =>
		new string([.. value.Where(char.IsLetterOrDigit)]).ToLowerInvariant() is { Length: > 0 } cleaned ? cleaned[..Math.Min(24, cleaned.Length)] : "x";

	private static string Body(JsonNode response)
	{
		var body = response["body"];
		var raw = body?.GetValueKind() == JsonValueKind.String ? body.GetValue<string>() : response["jsonBody"]?.ToJsonString() ?? string.Empty;
		return Prettify(raw);
	}

	private static string Prettify(string json)
	{
		try
		{
			return JsonNode.Parse(json)?.ToJsonString(Pretty) ?? json;
		}
		catch (JsonException)
		{
			return json;
		}
	}

	private static string ContentType(JsonNode? headers)
	{
		var value = (headers as JsonObject)?.FirstOrDefault(header => string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)).Value;
		return value?.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : "application/json";
	}
}
