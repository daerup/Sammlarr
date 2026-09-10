using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Tests.Recording;

internal sealed record RecordedResponse(string Provider, string Slug, string Path, string Body);

internal static class RecordedStubs
{
	private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

	internal static IReadOnlyList<RecordedResponse> Write(string snapshotJson, string mappingsDirectory)
	{
		var recorded = new List<RecordedResponse>();
		foreach (var mapping in JsonNode.Parse(snapshotJson)?["mappings"]?.AsArray() ?? [])
		{
			var request = mapping?["request"];
			var urlPath = request?["urlPath"]?.GetValue<string>() ?? request?["url"]?.GetValue<string>();
			if (urlPath is null)
			{
				continue;
			}

			var provider = ProviderRecording
				.AllEndpoints.OrderByDescending(candidate => candidate.Prefix.Length)
				.FirstOrDefault(candidate => urlPath.StartsWith($"{candidate.Prefix}/", StringComparison.Ordinal));
			if (provider is null)
			{
				continue;
			}

			var replayPath = urlPath.Split('?')[0][provider.Prefix.Length..];
			if (replayPath.Length == 0)
			{
				replayPath = "/";
			}

			var slug = Slug(replayPath);
			var response = mapping!["response"]!;

			Directory.CreateDirectory(Path.Combine(mappingsDirectory, provider.Name));
			var clean = new JsonObject
			{
				["request"] = new JsonObject { ["method"] = request!["method"]?.GetValue<string>() ?? "GET", ["urlPath"] = replayPath },
				["response"] = new JsonObject
				{
					["status"] = response["status"]?.GetValue<int>() ?? 200,
					["headers"] = new JsonObject { ["Content-Type"] = ContentType(response["headers"]) },
					["bodyFileName"] = $"{provider.Name}/{slug}.verified.json",
				},
			};
			File.WriteAllText(Path.Combine(mappingsDirectory, provider.Name, $"{slug}.json"), clean.ToJsonString(Pretty));

			recorded.Add(new RecordedResponse(provider.Name, slug, replayPath, Body(response)));
		}

		return recorded;
	}

	private static string Slug(string replayPath) => replayPath.Trim('/').Replace('/', '-') is { Length: > 0 } slug ? slug : "root";

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
