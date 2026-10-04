using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Contracts.Ratings;
using Domain.Media;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace API.Wikidata;

internal sealed partial class WikidataClient(HttpClient http) : IRottenTomatoesPageFinder
{
	private const string Accept = "application/sparql-results+json";

	public async Task<Result<Optional<string>>> FindPathAsync(ImdbId imdb, CancellationToken cancellationToken = default)
	{
		if (!ImdbPattern().IsMatch(imdb.Value))
		{
			return Error.From($"'{imdb.Value}' is not an IMDb title id.");
		}

		try
		{
			return await AskAsync(imdb.Value, cancellationToken);
		}
		catch (Exception ex) when (ex is HttpRequestException or BrokenCircuitException or TimeoutRejectedException or JsonException)
		{
			return Error.From($"Wikidata could not be asked for {imdb.Value}: {ex.Message}");
		}
	}

	private async Task<Result<Optional<string>>> AskAsync(string imdb, CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, $"sparql?query={Uri.EscapeDataString(Query(imdb))}");
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(Accept));
		using var response = await http.SendAsync(request, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			return Error.From($"Wikidata answered {(int)response.StatusCode} for {imdb}.");
		}

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var answer = await JsonSerializer.DeserializeAsync<SparqlAnswer>(stream, cancellationToken: cancellationToken);
		return Success.Of(PathOf(answer?.Results?.Bindings ?? []));
	}

	private static string Query(string imdb) => $$"""SELECT ?path WHERE { ?item wdt:P345 "{{imdb}}" ; wdt:P1258 ?path . }""";

	private static Optional<string> PathOf(IEnumerable<SparqlBinding> bindings)
	{
		return bindings
			.Select(binding => binding.Path?.Value ?? string.Empty)
			.Select(path => string.Join('/', path.Split('/').Take(2)))
			.Order(StringComparer.Ordinal)
			.FirstOrNone(PagePattern().IsMatch);
	}

	[GeneratedRegex(@"^tt[0-9]+\z")]
	private static partial Regex ImdbPattern();

	[GeneratedRegex(@"^(m|tv)/[a-z0-9_\-]+\z")]
	private static partial Regex PagePattern();

	private sealed record SparqlAnswer([property: JsonPropertyName("results")] SparqlResults? Results);

	private sealed record SparqlResults([property: JsonPropertyName("bindings")] IReadOnlyList<SparqlBinding>? Bindings);

	private sealed record SparqlBinding([property: JsonPropertyName("path")] SparqlValue? Path);

	private sealed record SparqlValue([property: JsonPropertyName("value")] string Value);
}
