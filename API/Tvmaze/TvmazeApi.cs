using System.Net;
using System.Net.Http.Json;
using API.Http;
using API.Tvmaze.Types;
using Contracts.Caching;
using Domain.Media;
using Domain.Providers;

namespace API.Tvmaze;

internal sealed class TvmazeApi(HttpClient http, ITypedMemoryCache<string, TvmazeTypes.Show> shows)
{
	private static readonly TimeSpan Remembered = TimeSpan.FromMinutes(10);

	public Task<Result<Optional<TvmazeTypes.Show>>> LookUpAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		return Query(id)
			.Match(
				query => shows.GetValueOrNone(query).Match(show => Task.FromResult(Found(Some.Of(show))), () => FetchAsync(query, cancellationToken)),
				() => Task.FromResult(Found(new None()))
			);
	}

	private static Optional<string> Query(IExternalMediaId id) => Optional.When(id.Type is not MediaType.Movie, () => id).Map(Path);

	private static Optional<string> Path(IExternalMediaId id)
	{
		return id.Source switch
		{
			MediaProvider.Tvmaze => Some.Of($"shows/{id.Value}"),
			MediaProvider.Imdb => Some.Of($"lookup/shows?imdb={Uri.EscapeDataString(id.Value)}"),
			MediaProvider.Tvdb => Some.Of($"lookup/shows?thetvdb={Uri.EscapeDataString(id.Value)}"),
			MediaProvider.Tmdb
			or MediaProvider.RottenTomatoes
			or MediaProvider.Plex
			or MediaProvider.Mdblist
			or MediaProvider.Metacritic
			or MediaProvider.Letterboxd
			or MediaProvider.Trakt
			or MediaProvider.Mal => new None(),
		};
	}

	private async Task<Result<Optional<TvmazeTypes.Show>>> FetchAsync(string query, CancellationToken cancellationToken)
	{
		using var response = await http.GetAsync(query, cancellationToken);
		if (response.StatusCode is HttpStatusCode.NotFound)
		{
			return Found(new None());
		}

		if (!response.IsSuccessStatusCode)
		{
			return response.StatusCode.Unanswered(RemoteService.Tvmaze, $"TVmaze answered {(int)response.StatusCode} for {query}.");
		}

		var show = Optional.Of(await response.Content.ReadFromJsonAsync<TvmazeTypes.Show>(cancellationToken));
		return Found(show.Apply(found => shows.Set(query, found, Remembered)));
	}

	private static Result<Optional<TvmazeTypes.Show>> Found(Optional<TvmazeTypes.Show> show) => Success.Of(show);
}
