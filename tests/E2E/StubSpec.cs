namespace E2E.Tests;

internal sealed record StubSpec(string UrlPath, int Status, string Body)
{
	internal string Method { get; init; } = "GET";

	internal IReadOnlyDictionary<string, string> Query { get; init; } = new Dictionary<string, string>();

	internal IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

	internal IReadOnlyList<string> BodyContains { get; init; } = [];

	internal int DelayMs { get; init; }
}
