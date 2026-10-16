using System.Text.Json;

namespace E2E.Tests;

internal static class WireMockStubs
{
	private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "wiremock"));

	private static readonly JsonSerializerOptions PolicyJson = new() { PropertyNameCaseInsensitive = true };

	private static readonly PolicyFile Policies = LoadPolicies();

	internal static string MappingsDirectory => Path.Combine(Root, "mappings");

	internal static string ResponsesDirectory => Path.Combine(Root, "responses");

	internal static FixturePolicy PolicyFor(string path) =>
		Matches(Policies.Frozen, path) ? FixturePolicy.Frozen
		: Matches(Policies.Contract, path) ? FixturePolicy.Contract
		: FixturePolicy.Value;

	private static bool Matches(IReadOnlyList<string> patterns, string path) =>
		patterns.Any(pattern =>
			pattern.EndsWith('*') ? path.StartsWith(pattern[..^1], StringComparison.Ordinal) : string.Equals(path, pattern, StringComparison.Ordinal)
		);

	private static PolicyFile LoadPolicies()
	{
		var file = Path.Combine(Root, "fixture-policy.json");
		return File.Exists(file) ? JsonSerializer.Deserialize<PolicyFile>(File.ReadAllText(file), PolicyJson) ?? new PolicyFile() : new PolicyFile();
	}

	private sealed record PolicyFile
	{
		public List<string> Contract { get; init; } = [];

		public List<string> Frozen { get; init; } = [];
	}
}
