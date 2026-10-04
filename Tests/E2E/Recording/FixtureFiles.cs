using System.Text.Json.Nodes;

namespace E2E.Tests.Recording;

internal static class FixtureFiles
{
	private static readonly Lock Gate = new();

	private static readonly HashSet<string> Captured = [];

	internal static bool Capture(RecordedResponse response)
	{
		lock (Gate)
		{
			return Captured.Add($"{response.Provider}/{response.Slug}");
		}
	}

	internal static async Task<IReadOnlyList<string>> StoreAsync(IEnumerable<RecordedResponse> recorded)
	{
		var committed = new List<RecordedResponse>();
		foreach (var response in recorded)
		{
			if (WireMockStubs.PolicyFor(response.Path) is FixturePolicy.Frozen || !await BootstrapAsync(response))
			{
				committed.Add(response);
			}
		}

		var drift = new List<string>();
		foreach (var response in committed.Where(response => File.Exists(FixturePath(response))))
		{
			drift.AddRange(await CompareAsync(response));
		}

		return drift;
	}

	private static async Task<bool> BootstrapAsync(RecordedResponse response)
	{
		var fixturePath = FixturePath(response);
		if (File.Exists(fixturePath))
		{
			return false;
		}

		Directory.CreateDirectory(Path.GetDirectoryName(fixturePath)!);
		await WriteFixtureAsync(fixturePath, response.Body);
		return true;
	}

	private static Task WriteFixtureAsync(string path, string body) => File.WriteAllTextAsync(path, body.ReplaceLineEndings("\n"));

	private static string FixturePath(RecordedResponse response)
	{
		return Path.Combine(WireMockStubs.ResponsesDirectory, response.Provider, $"{response.Slug}.verified.json");
	}

	private static async Task<IEnumerable<string>> CompareAsync(RecordedResponse response)
	{
		var fixturePath = FixturePath(response);
		var committed = await File.ReadAllTextAsync(fixturePath);
		var policy = WireMockStubs.PolicyFor(response.Path);
		var problems = policy is FixturePolicy.Value ? ValueDrift(response.Body, committed) : JsonContract.Incompatibilities(response.Body, committed);

		var receivedPath = $"{fixturePath[..^".verified.json".Length]}.received.json";
		if (problems.Count > 0)
		{
			await WriteFixtureAsync(receivedPath, response.Body);
		}
		else
		{
			File.Delete(receivedPath);
			if (policy is FixturePolicy.Contract)
			{
				await WriteFixtureAsync(fixturePath, response.Body);
			}
		}

		return problems.Select(problem => $"{response.Provider}/{response.Slug}: {problem}");
	}

	private static IReadOnlyList<string> ValueDrift(string live, string committed)
	{
		return string.Equals(live.ReplaceLineEndings("\n"), committed.ReplaceLineEndings("\n"), StringComparison.Ordinal)
			? []
			: ["the live response no longer matches the committed value"];
	}

	internal static async Task PruneAsync()
	{
		HashSet<string> captured;
		lock (Gate)
		{
			captured = [.. Captured];
		}

		var capturedProviders = captured.Select(key => key.Split('/')[0]).ToHashSet();
		foreach (var mappingFile in Directory.EnumerateFiles(WireMockStubs.MappingsDirectory, "*.json", SearchOption.AllDirectories))
		{
			var provider = Path.GetFileName(Path.GetDirectoryName(mappingFile)!);
			var slug = Path.GetFileNameWithoutExtension(mappingFile);
			if (provider == "mappings" || !capturedProviders.Contains(provider) || captured.Contains($"{provider}/{slug}"))
			{
				continue;
			}

			var mapped = JsonNode.Parse(await File.ReadAllTextAsync(mappingFile))?["request"]?["urlPath"]?.GetValue<string>();
			if (mapped is not null && WireMockStubs.PolicyFor(mapped) is FixturePolicy.Frozen)
			{
				continue;
			}

			var mapping = JsonNode.Parse(await File.ReadAllTextAsync(mappingFile));
			File.Delete(mappingFile);
			if (mapping?["response"]?["bodyFileName"]?.GetValue<string>() is { } bodyFileName)
			{
				File.Delete(Path.Combine(WireMockStubs.ResponsesDirectory, bodyFileName));
			}
		}
	}
}
