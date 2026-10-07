using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using API.Anime.Types;
using API.Http;
using Contracts.Datasets;
using Domain.Media;
using Domain.Providers;

namespace API.Anime;

internal sealed class AnimeIdsFile(HttpClient http) : IDataset<AnimeIdMapping>
{
	public string Name => DatasetNames.AnimeIds;

	public async Task<Result<Optional<DatasetFile<AnimeIdMapping>>>> DownloadAsync(Optional<string> etag, CancellationToken cancellationToken = default)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, "anime-list-full.json");
		etag.Apply(tag => request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(tag)));
		using var response = await http.SendAsync(request, cancellationToken);
		if (response.StatusCode is HttpStatusCode.NotModified)
		{
			return Success.Of<Optional<DatasetFile<AnimeIdMapping>>>(new None());
		}

		if (!response.IsSuccessStatusCode)
		{
			return response.StatusCode.Unanswered(RemoteService.AnimeLists, $"The anime id map answered {(int)response.StatusCode}.");
		}

		var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
		return Success.Of<Optional<DatasetFile<AnimeIdMapping>>>(Some.Of(new DatasetFile<AnimeIdMapping>(Optional.Of(response.Headers.ETag?.Tag), Parse(body))));
	}

	internal static async IAsyncEnumerable<AnimeIdMapping> Parse(byte[] body, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var claimed = new HashSet<string>(StringComparer.Ordinal);
		await foreach (var entry in JsonSerializer.DeserializeAsyncEnumerable<AnimeListsTypes.Anime>(new MemoryStream(body), cancellationToken: cancellationToken))
		{
			var read = entry ?? throw new InvalidDataException("The anime id map holds an entry it cannot read.");
			if (read.AniList is not null || read.Mal is not null)
			{
				yield return Of(read, claimed);
			}
		}
	}

	private static AnimeIdMapping Of(AnimeListsTypes.Anime entry, HashSet<string> claimed)
	{
		var movie = FirstMovie(entry.Tmdb);
		var type = movie.Match(_ => MediaType.Movie, () => entry.Type == "MOVIE" ? MediaType.Movie : MediaType.Show);
		var whole = type is MediaType.Movie || IsTheShowItself(entry);
		var tvdb = Claim(claimed, "tvdb", Optional.Of(entry.Tvdb).When(_ => type is MediaType.Show && whole));
		var tmdb = Claim(claimed, $"tmdb-{type}", type is MediaType.Movie ? movie : Optional.Of(entry.Tmdb?.Tv).When(_ => whole));
		var imdb = Claim(claimed, "imdb", Optional.Of(entry.Imdb?.FirstOrDefault()).When(_ => whole));
		return new AnimeIdMapping(type, Optional.Of(entry.AniList), Optional.Of(entry.Mal), tvdb, tmdb, imdb);
	}

	private static bool IsTheShowItself(AnimeListsTypes.Anime entry)
	{
		return entry.Season?.Tvdb is null or 1
			&& entry.Season?.Tmdb is null or 1
			&& entry.EpisodeOffset?.Tvdb is null or 0
			&& entry.EpisodeOffset?.Tmdb is null or 0;
	}

	private static Optional<int> FirstMovie(AnimeListsTypes.Tmdb? tmdb)
	{
		return Optional
			.Of(tmdb?.Movie)
			.Map(movie =>
				movie.ValueKind switch
				{
					JsonValueKind.Number => Some.Of(movie.GetInt32()),
					JsonValueKind.Array => movie.EnumerateArray().Where(each => each.ValueKind is JsonValueKind.Number).Select(each => each.GetInt32()).FirstOrNone(),
					JsonValueKind.Undefined or JsonValueKind.Object or JsonValueKind.String or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null =>
						new None(),
				}
			);
	}

	private static Optional<T> Claim<T>(HashSet<string> claimed, string kind, Optional<T> id)
		where T : notnull
	{
		return id.When(found => claimed.Add($"{kind}:{found}"));
	}
}
