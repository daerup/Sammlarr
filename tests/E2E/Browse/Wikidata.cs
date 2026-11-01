namespace E2E.Tests.Browse;

internal static class Wikidata
{
	internal static StubSpec Sparql(string imdb, params string[] paths)
	{
		var bindings = string.Join(
			',',
			paths.Select(path => $$$"""{"imdb":{"type":"literal","value":"{{{imdb}}}"},"rt":{"type":"literal","value":"{{{path}}}"}}""")
		);
		return new StubSpec("/sparql", 200, $$$"""{"results":{"bindings":[{{{bindings}}}]}}""") { Method = "POST", BodyContains = [$"%22{imdb}%22"] };
	}
}
