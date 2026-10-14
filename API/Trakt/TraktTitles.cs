using System.Net;
using API.Http;
using API.Trakt.Types;
using Contracts;
using Contracts.Auth;
using Domain.Auth;
using Domain.Media;
using Domain.Providers;

namespace API.Trakt;

internal sealed class TraktTitles(TraktClient client, IApiKeyService apiKeys, IDateTimeProvider clock)
{
	private readonly RecentAnswers<string, Optional<TraktTypes.Title>> _found = new(clock);

	internal Task<Result<Optional<TraktTypes.Title>>> FindAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		return _found.AskAsync(Key(id), token => AskAsync(id, token), cancellationToken);
	}

	private void Remember(TraktTypes.Title title, MediaType type)
	{
		foreach (var id in TraktTitle.Ids(title, type))
		{
			_found.Remember(Key(id), Some.Of(title));
		}
	}

	private static string Key(IExternalMediaId id) => $"{id.Source}:{id.Type}:{id.Value}";

	private async Task<Result<Optional<TraktTypes.Title>>> AskAsync(IExternalMediaId id, CancellationToken cancellationToken)
	{
		var clientId = await apiKeys.GetAsync(ApiKeyProvider.Trakt, cancellationToken);
		var answer = await clientId.Map(found => Endpoint(id).Map(endpoint => ReadAsync(id, endpoint, found, cancellationToken)));
		return answer.Apply(title => title.Apply(found => Remember(found, id.Type)));
	}

	private static Result<string> Endpoint(IExternalMediaId id)
	{
		var kind = TraktTitle.Kind(id.Type);
		var type = id.Type is MediaType.Show ? "show" : "movie";
		return id.Type is MediaType.Unknown
			? Error.From($"Trakt is asked for a movie or a show, not for {id}.")
			: id.Source switch
			{
				MediaProvider.Trakt or MediaProvider.Imdb => Success.Of($"{kind}/{Uri.EscapeDataString(id.Value)}?extended=full"),
				MediaProvider.Tmdb => Success.Of($"search/tmdb/{Uri.EscapeDataString(id.Value)}?type={type}&extended=full"),
				MediaProvider.Tvdb => Success.Of($"search/tvdb/{Uri.EscapeDataString(id.Value)}?type={type}&extended=full"),
				MediaProvider.Plex
				or MediaProvider.RottenTomatoes
				or MediaProvider.Mdblist
				or MediaProvider.Metacritic
				or MediaProvider.Letterboxd
				or MediaProvider.Mal
				or MediaProvider.Tvmaze
				or MediaProvider.AniList => Error.From($"Trakt looks up no {id.Source} ids."),
			};
	}

	private async Task<Result<Optional<TraktTypes.Title>>> ReadAsync(IExternalMediaId id, string endpoint, IApiKey clientId, CancellationToken cancellationToken)
	{
		using var response = await client.GetAsync(endpoint, clientId, cancellationToken);
		if (response.StatusCode is HttpStatusCode.NotFound)
		{
			return Success.Of<Optional<TraktTypes.Title>>(new None());
		}

		if (TraktClient.IsBlocked(response))
		{
			return Error.From(new RemoteUnavailableException(RemoteService.Trakt, sent: true));
		}

		if (!response.IsSuccessStatusCode)
		{
			return response.StatusCode.Unanswered(RemoteService.Trakt, FormattableString.CurrentCulture($"Trakt answered {(int)response.StatusCode} for {id}."));
		}

		return endpoint.StartsWith("search/", StringComparison.Ordinal)
			? Success.Of(await SearchedAsync(response, id.Type, cancellationToken))
			: Success.Of(Optional.Of(await response.Content.ReadFromJsonAsync<TraktTypes.Title>(cancellationToken)));
	}

	private static async Task<Optional<TraktTypes.Title>> SearchedAsync(HttpResponseMessage response, MediaType type, CancellationToken cancellationToken)
	{
		var entries =
			await response.Content.ReadFromJsonAsync<List<TraktTypes.Item>>(cancellationToken) ?? throw new InvalidDataException("Trakt answered an empty search.");
		return entries.Select(entry => Optional.Of(type is MediaType.Show ? entry.Show : entry.Movie)).WhereSome().FirstOrNone();
	}
}
