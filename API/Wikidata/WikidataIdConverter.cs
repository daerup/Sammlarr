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

	private static readonly ById ByImdb = new(
		Accepts: [MediaProvider.Imdb],
		Yields:
		[
			MediaProvider.RottenTomatoes,
			MediaProvider.Tmdb,
			MediaProvider.Tvdb,
			MediaProvider.Metacritic,
			MediaProvider.Letterboxd,
			MediaProvider.AniList,
			MediaProvider.Mal,
		],
		Cost: 3,
		GivesHints: false
	);

	public IReadOnlyList<IdLookup> Lookups { get; } = [ByImdb];

	public Task<Answer<IdLookupResult>> LookUpAsync(IdLookupRequest request, CancellationToken cancellationToken = default)
	{
		return LookUpResultAsync(request, cancellationToken).GuardedAsync(RemoteService.Wikidata);
	}

	private async Task<Result<IdLookupResult>> LookUpResultAsync(IdLookupRequest request, CancellationToken cancellationToken)
	{
		return request switch
		{
			ByIdRequest(var lookup, ImdbId imdb) when lookup == ByImdb => await IdsAsync(imdb, cancellationToken),
			_ => Error.From("Wikidata does not offer this lookup."),
		};
	}

	private static string Query(ImdbId imdb)
	{
		var typed = imdb.Type switch
		{
			MediaType.Movie => "OPTIONAL { ?item wdt:P4947 ?tmdb . } OPTIONAL { ?item wdt:P12196 ?tvdb . } ",
			MediaType.Show => "OPTIONAL { ?item wdt:P4983 ?tmdb . } OPTIONAL { ?item wdt:P4835 ?tvdb . } ",
			MediaType.Unknown => string.Empty,
		};
		return $$"""SELECT ?rt ?tmdb ?tvdb ?metacritic ?letterboxd ?anilist ?mal WHERE { ?item wdt:P345 "{{imdb.Value}}" . OPTIONAL { ?item wdt:P1258 ?rt . } {{typed}}OPTIONAL { ?item wdt:P1712 ?metacritic . } OPTIONAL { ?item wdt:P6127 ?letterboxd . } OPTIONAL { ?item wdt:P8729 ?anilist . } OPTIONAL { ?item wdt:P4086 ?mal . } }""";
	}

	private async Task<Result<IdLookupResult>> IdsAsync(ImdbId imdb, CancellationToken cancellationToken)
	{
		if (!ImdbPattern().IsMatch(imdb.Value))
		{
			return Error.From($"'{imdb.Value}' is not an IMDb title id.");
		}

		return await AskAsync(imdb, cancellationToken).Map(rows => new IdLookupResult(Ids(rows, imdb.Type), new None()));
	}

	private async Task<Result<IReadOnlyList<Dictionary<string, SparqlValue>>>> AskAsync(ImdbId imdb, CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, $"sparql?query={Uri.EscapeDataString(Query(imdb))}");
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(Accept));
		using var response = await http.SendAsync(request, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			return response.StatusCode.Unanswered(RemoteService.Wikidata, $"Wikidata answered {(int)response.StatusCode} for {imdb.Value}.");
		}

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var answer = await JsonSerializer.DeserializeAsync<SparqlAnswer>(stream, cancellationToken: cancellationToken);
		return Success.Of(answer?.Results?.Bindings ?? []);
	}

	private static IReadOnlyList<IExternalMediaId> Ids(IReadOnlyList<Dictionary<string, SparqlValue>> rows, MediaType type)
	{
		Optional<IExternalMediaId>[] found =
		[
			First(rows, "rt", RottenTomatoesPage()).Map(path => Id(RottenTomatoesId.From(path, type))),
			First(rows, "tmdb", Number()).Map(value => Id(TmdbId.From(value, type))),
			First(rows, "tvdb", Number()).Map(value => Id(TvdbId.From(value, type))),
			First(rows, "metacritic", MetacriticPage()).Map(page => Id(MetacriticId.From(page[(page.IndexOf('/') + 1)..], type))),
			First(rows, "letterboxd", Slug()).Map(slug => Id(LetterboxdId.From(slug, type))),
			First(rows, "anilist", Number()).Map(value => Id(AniListId.From(value, type))),
			First(rows, "mal", Number()).Map(value => Id(MalId.From(value, type))),
		];
		return [.. found.WhereSome()];
	}

	private static Optional<string> First(IReadOnlyList<Dictionary<string, SparqlValue>> rows, string name, Regex valid)
	{
		return rows.Select(row => row.GetValueOrNone(name).Map(value => value.Value)).WhereSome().Order(StringComparer.Ordinal).FirstOrNone(valid.IsMatch);
	}

	private static IExternalMediaId Id(IExternalMediaId id) => id;

	[GeneratedRegex(@"^tt[0-9]+\z")]
	private static partial Regex ImdbPattern();

	[GeneratedRegex(@"^(m|tv)/[a-z0-9_\-]+\z")]
	private static partial Regex RottenTomatoesPage();

	[GeneratedRegex(@"^[0-9]+\z")]
	private static partial Regex Number();

	[GeneratedRegex(@"^(movie|tv)/[a-z0-9\-]+\z")]
	private static partial Regex MetacriticPage();

	[GeneratedRegex(@"^[a-z0-9\-]+\z")]
	private static partial Regex Slug();

	private sealed record SparqlAnswer([property: JsonPropertyName("results")] SparqlResults? Results);

	private sealed record SparqlResults([property: JsonPropertyName("bindings")] IReadOnlyList<Dictionary<string, SparqlValue>>? Bindings);

	private sealed record SparqlValue([property: JsonPropertyName("value")] string Value);
}
