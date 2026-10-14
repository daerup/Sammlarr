using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using API.Http;
using API.Http.Lanes;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;

namespace API.Wikidata;

internal sealed partial class WikidataIdConverter(HttpClient http, Lanes lanes) : IExternalIdConverter
{
	private const string Accept = "application/sparql-results+json";

	private static readonly LaneName Sparql = new(RemoteService.Wikidata, "sparql");

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

	private static string Query(MediaType type, IReadOnlyList<ImdbId> ids)
	{
		var typed = type switch
		{
			MediaType.Movie => "OPTIONAL { ?item wdt:P4947 ?tmdb . } OPTIONAL { ?item wdt:P12196 ?tvdb . } ",
			MediaType.Show => "OPTIONAL { ?item wdt:P4983 ?tmdb . } OPTIONAL { ?item wdt:P4835 ?tvdb . } ",
			MediaType.Unknown => string.Empty,
		};
		var values = string.Join(' ', ids.Select(id => $"\"{id.Value}\""));
		return $$"""SELECT ?imdb ?rt ?tmdb ?tvdb ?metacritic ?letterboxd ?anilist ?mal WHERE { VALUES ?imdb { {{values}} } ?item wdt:P345 ?imdb . OPTIONAL { ?item wdt:P1258 ?rt . } {{typed}}OPTIONAL { ?item wdt:P1712 ?metacritic . } OPTIONAL { ?item wdt:P6127 ?letterboxd . } OPTIONAL { ?item wdt:P8729 ?anilist . } OPTIONAL { ?item wdt:P4086 ?mal . } }""";
	}

	private async Task<Result<IdLookupResult>> IdsAsync(ImdbId imdb, CancellationToken cancellationToken)
	{
		if (!ImdbPattern.IsMatch(imdb.Value))
		{
			return Error.From($"'{imdb.Value}' is not an IMDb title id.");
		}

		var lane = lanes.Of<ImdbId, IReadOnlyList<IExternalMediaId>>(Sparql);
		var answer = await lane.AskAsync(imdb, new LaneCall<ImdbId, IReadOnlyList<IExternalMediaId>>(id => $"{id.Type}", AskAsync), cancellationToken);
		return answer switch
		{
			LaneHit<IReadOnlyList<IExternalMediaId>> hit => Success.Of(new IdLookupResult(hit.Value, new None())),
			LaneMiss => Success.Of(new IdLookupResult([], new None())),
			LaneFailed failed => failed.Cause,
		};
	}

	private async Task<Result<IReadOnlyDictionary<ImdbId, IReadOnlyList<IExternalMediaId>>>> AskAsync(
		IReadOnlyList<ImdbId> ids,
		CancellationToken cancellationToken
	)
	{
		var type = ids[0].Type;
		using var request = new HttpRequestMessage(HttpMethod.Post, "sparql") { Content = new FormUrlEncodedContent([new("query", Query(type, ids))]) };
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(Accept));
		using var response = await http.SendAsync(request, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			return response.StatusCode.Unanswered(RemoteService.Wikidata, $"Wikidata answered {(int)response.StatusCode} for {ids.Count} titles.");
		}

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var answer = await JsonSerializer.DeserializeAsync<SparqlAnswer>(stream, cancellationToken: cancellationToken);
		var byImdb = (answer?.Results?.Bindings ?? []).ToLookup(row => row.GetValueOrNone("imdb").Map(value => value.Value).GetValueOr(string.Empty));
		return Success.Of<IReadOnlyDictionary<ImdbId, IReadOnlyList<IExternalMediaId>>>(
			ids.Where(id => byImdb.Contains(id.Value)).ToDictionary(id => id, id => Ids([.. byImdb[id.Value]], type))
		);
	}

	private static IReadOnlyList<IExternalMediaId> Ids(IReadOnlyList<Dictionary<string, SparqlValue>> rows, MediaType type)
	{
		Optional<IExternalMediaId>[] found =
		[
			First(rows, "rt", RottenTomatoesPage).Map(path => Id(RottenTomatoesId.From(path, type))),
			First(rows, "tmdb", Number).Map(value => Id(TmdbId.From(value, type))),
			First(rows, "tvdb", Number).Map(value => Id(TvdbId.From(value, type))),
			First(rows, "metacritic", MetacriticPage).Map(page => Id(MetacriticId.From(page[(page.IndexOf('/') + 1)..], type))),
			First(rows, "letterboxd", Slug).Map(slug => Id(LetterboxdId.From(slug, type))),
			First(rows, "anilist", Number).Map(value => Id(AniListId.From(value, type))),
			First(rows, "mal", Number).Map(value => Id(MalId.From(value, type))),
		];
		return [.. found.WhereSome()];
	}

	private static Optional<string> First(IReadOnlyList<Dictionary<string, SparqlValue>> rows, string name, Regex valid)
	{
		return rows.Select(row => row.GetValueOrNone(name).Map(value => value.Value)).WhereSome().Order(StringComparer.Ordinal).FirstOrNone(valid.IsMatch);
	}

	private static IExternalMediaId Id(IExternalMediaId id) => id;

	[GeneratedRegex(@"^tt[0-9]+\z", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
	private static partial Regex ImdbPattern { get; }

	[GeneratedRegex(@"^(m|tv)/[a-z0-9_\-]+\z", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
	private static partial Regex RottenTomatoesPage { get; }

	[GeneratedRegex(@"^[0-9]+\z", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
	private static partial Regex Number { get; }

	[GeneratedRegex(@"^(movie|tv)/[a-z0-9\-]+\z", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
	private static partial Regex MetacriticPage { get; }

	[GeneratedRegex(@"^[a-z0-9\-]+\z", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
	private static partial Regex Slug { get; }

	private sealed record SparqlAnswer([property: JsonPropertyName("results")] SparqlResults? Results);

	private sealed record SparqlResults([property: JsonPropertyName("bindings")] IReadOnlyList<Dictionary<string, SparqlValue>>? Bindings);

	private sealed record SparqlValue([property: JsonPropertyName("value")] string Value);
}
