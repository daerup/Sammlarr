using System.Net;
using System.Text.Json.Nodes;

namespace E2E.Tests.Recording;

internal sealed record Recordable(ProviderEndpoint Endpoint, string PathAndQuery)
{
	internal string SnapshotUrl => $"{Endpoint.Prefix}{PathAndQuery}";

	internal string SecretName => $"{Endpoint.Name.ToUpperInvariant()}_API_KEY";

	internal bool NeedsSecret => Query.Any(pair => IsCredential(pair[0]));

	private string[][] Query => PathAndQuery.Split('?', 2) is [_, var query] ? [.. query.Split('&').Select(pair => pair.Split('=', 2))] : [];

	internal string RealUrl(string secret)
	{
		var path = PathAndQuery.Split('?', 2)[0];
		var query = Query.Select(pair => IsCredential(pair[0]) ? $"{pair[0]}={Uri.EscapeDataString(secret)}" : string.Join('=', pair)).ToList();
		var realPath = query.Count == 0 ? path : $"{path}?{string.Join('&', query)}";
		return $"{Endpoint.RealBaseUrl.TrimEnd('/')}{realPath}";
	}

	private static bool IsCredential(string name) => RecordedStubs.CredentialParams.Contains(name, StringComparer.OrdinalIgnoreCase);
}

internal static class MetadataRecorder
{
	private static readonly IReadOnlyList<ProviderEndpoint> Providers = [.. ProviderRecording.PlexFree.SelectMany(provider => provider.Endpoints)];

	internal static Recordable? Find(string method, string url)
	{
		var uri = new Uri(url);
		if (method != "GET")
		{
			return null;
		}

		if (ProviderRecording.ArrMetadata.FirstOrDefault(endpoint => endpoint.Host == uri.Host) is { } metadata)
		{
			return new Recordable(metadata, uri.PathAndQuery);
		}

		return Providers.FirstOrDefault(endpoint => uri.AbsolutePath.StartsWith($"{endpoint.Prefix}/", StringComparison.Ordinal)) is { } provider
			? new Recordable(provider, uri.PathAndQuery[provider.Prefix.Length..])
			: null;
	}

	internal static async Task<IReadOnlyList<string>> RecordAsync(IEnumerable<Recordable> recordables)
	{
		using var http = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });
		http.DefaultRequestHeaders.UserAgent.ParseAdd("Sammlarr-fixture-recorder");
		var mappings = new JsonArray();
		var refused = new List<string>();
		foreach (var recordable in recordables)
		{
			var secret = RecordingConfiguration.Value(recordable.SecretName);
			if (recordable.NeedsSecret && string.IsNullOrEmpty(secret))
			{
				refused.Add($"{recordable.SnapshotUrl}: {recordable.SecretName} is not in user-secrets");
				continue;
			}

			using var response = await http.GetAsync(recordable.RealUrl(secret ?? string.Empty));
			if (!response.IsSuccessStatusCode)
			{
				refused.Add($"{recordable.SnapshotUrl}: the real host answered {(int)response.StatusCode}");
				continue;
			}

			var contentType = response.Content.Headers.NonValidated.TryGetValues("Content-Type", out var raw) ? raw.ToString() : "application/json";
			mappings.Add(
				new JsonObject
				{
					["request"] = new JsonObject { ["method"] = "GET", ["url"] = recordable.SnapshotUrl },
					["response"] = new JsonObject
					{
						["status"] = (int)response.StatusCode,
						["headers"] = new JsonObject { ["Content-Type"] = contentType },
						["body"] = await response.Content.ReadAsStringAsync(),
					},
				}
			);
		}

		var recorded = RecordedStubs.Write(new JsonObject { ["mappings"] = mappings }.ToJsonString(), WireMockStubs.MappingsDirectory, _ => false);
		return [.. refused, .. await FixtureFiles.StoreAsync(recorded.Where(FixtureFiles.Capture).ToList())];
	}
}
