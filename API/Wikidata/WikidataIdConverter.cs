using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using API.Http;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;

namespace API.Wikidata;

internal sealed partial class WikidataIdConverter(HttpClient http) : IExternalIdConverter
{
	private const string Accept = "application/sparql-results+json";

	private static readonly ById ByImdb = new(Accepts: [MediaProvider.Imdb], Yields: [MediaProvider.RottenTomatoes], Cost: 1, GivesHints: false);

	public IReadOnlyList<IdLookup> Lookups { get; } = [ByImdb];

	public async Task<Result<IdLookupResult>> LookUpAsync(IdLookupRequest request, CancellationToken cancellationToken = default)
	{
		return request switch
		{
			ByIdRequest(var lookup, ImdbId imdb) when lookup == ByImdb => await PageAsync(imdb, cancellationToken),
			_ => Error.From("Wikidata does not offer this lookup."),
		};
	}

	private async Task<Result<IdLookupResult>> PageAsync(ImdbId imdb, CancellationToken cancellationToken)
	{
		if (!ImdbPattern().IsMatch(imdb.Value))
		{
			return Error.From($"'{imdb.Value}' is not an IMDb title id.");
		}

		return await AskAsync(imdb.Value, cancellationToken).Map(path => new IdLookupResult(Ids(path, imdb.Type), new None()));
	}

	private async Task<Result<Optional<string>>> AskAsync(string imdb, CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, $"sparql?query={Uri.EscapeDataString(Query(imdb))}");
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(Accept));
		using var response = await http.SendAsync(request, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			return response.StatusCode.Unanswered(RemoteService.Wikidata, $"Wikidata answered {(int)response.StatusCode} for {imdb}.");
		}

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var answer = await JsonSerializer.DeserializeAsync<SparqlAnswer>(stream, cancellationToken: cancellationToken);
		return Success.Of(PathOf(answer?.Results?.Bindings ?? []));
	}

	private static IReadOnlyList<IExternalMediaId> Ids(Optional<string> path, MediaType type)
	{
		return path.Map(found => (IReadOnlyList<IExternalMediaId>)[RottenTomatoesId.From(found, type)]).GetValueOr([]);
	}

	private static string Query(string imdb) => $$"""SELECT ?path WHERE { ?item wdt:P345 "{{imdb}}" ; wdt:P1258 ?path . }""";

	private static Optional<string> PathOf(IEnumerable<SparqlBinding> bindings)
	{
		return bindings.Select(binding => binding.Path?.Value ?? string.Empty).Order(StringComparer.Ordinal).FirstOrNone(PagePattern().IsMatch);
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
