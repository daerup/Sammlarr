using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Harness;

internal sealed partial class WikidataStub
{
	private readonly HttpStub _http;

	private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _held = new();

	private readonly ConcurrentDictionary<string, byte> _failing = new();

	private readonly ConcurrentDictionary<string, int> _asked = new();

	public WikidataStub(HttpStub http)
	{
		_http = http;
		http.RespondForUrlAsync($"{TestUrls.WikidataApi}/sparql", AnswerAsync);
	}

	public void Holds(string imdb, IReadOnlyList<string> rows)
	{
		_failing.TryRemove(imdb, out _);
		_held[imdb] = rows;
	}

	public void Fails(string imdb) => _failing[imdb] = 0;

	public int Asked(string imdb) => _asked.GetValueOrDefault(imdb);

	private async Task<HttpResponseMessage> AnswerAsync(HttpRequestMessage request)
	{
		var form = await request.Content!.ReadAsStringAsync();
		var query = WebUtility.UrlDecode(form["query=".Length..]);
		List<string> asked = [.. ImdbIds().Matches(query).Select(match => match.Groups[1].Value)];
		asked.ForEach(imdb => _asked.AddOrUpdate(imdb, 1, (_, count) => count + 1));
		if (asked.Any(_failing.ContainsKey))
		{
			return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
		}

		asked.Where(imdb => !_held.ContainsKey(imdb)).ToList().ForEach(imdb => _http.Unexpect($"wikidata {imdb}"));
		var rows = asked.Where(_held.ContainsKey).SelectMany(imdb => _held[imdb].Select(row => WithImdb(imdb, row)));
		var json = $$$"""{"results":{"bindings":[{{{string.Join(',', rows)}}}]}}""";
		return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/sparql-results+json") };
	}

	private static string WithImdb(string imdb, string row) => $$$"""{"imdb":{"type":"literal","value":"{{{imdb}}}"},{{{row[1..]}}}""";

	[GeneratedRegex("\"(tt[0-9]+)\"")]
	private static partial Regex ImdbIds();
}
