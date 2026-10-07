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
	private static readonly TimeSpan Kept = TimeSpan.FromMinutes(10);

	private readonly Lock _gate = new();

	private readonly Dictionary<string, Found> _found = [];

	internal Task<Result<Optional<TraktTypes.Title>>> FindAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		var key = Key(id);
		lock (_gate)
		{
			return _found
				.GetValueOrNone(key)
				.When(found => clock.Now - found.At < Kept)
				.Match(found => found.Answer, () => Start(key, id))
				.WaitAsync(cancellationToken);
		}
	}

	private Task<Result<Optional<TraktTypes.Title>>> Start(string key, IExternalMediaId id)
	{
		var answer = AskAsync(id);
		_found[key] = new Found(answer, clock.Now);
		_ = SettleAsync(key, id.Type, answer);
		return answer;
	}

	private async Task SettleAsync(string key, MediaType type, Task<Result<Optional<TraktTypes.Title>>> answer)
	{
		await ((Task)answer).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
		Result<Optional<TraktTypes.Title>> settled = answer.IsCompletedSuccessfully ? answer.Result : Error.From("Trakt's lookup threw.");
		settled.Apply(title => title.Apply(found => Remember(found, type, answer))).ApplyError(_ => Forget(key));
	}

	private void Remember(TraktTypes.Title title, MediaType type, Task<Result<Optional<TraktTypes.Title>>> answer)
	{
		lock (_gate)
		{
			foreach (var id in TraktTitle.Ids(title, type))
			{
				_found[Key(id)] = new Found(answer, clock.Now);
			}
		}
	}

	private void Forget(string key)
	{
		lock (_gate)
		{
			_found.Remove(key);
		}
	}

	private static string Key(IExternalMediaId id) => $"{id.Source}:{id.Type}:{id.Value}";

	private async Task<Result<Optional<TraktTypes.Title>>> AskAsync(IExternalMediaId id)
	{
		var clientId = await apiKeys.GetAsync(ApiKeyProvider.Trakt);
		return await clientId.Map(found => Endpoint(id).Map(endpoint => ReadAsync(id, endpoint, found)));
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

	private async Task<Result<Optional<TraktTypes.Title>>> ReadAsync(IExternalMediaId id, string endpoint, IApiKey clientId)
	{
		using var response = await client.GetAsync(endpoint, clientId);
		if (response.StatusCode is HttpStatusCode.NotFound)
		{
			return Success.Of<Optional<TraktTypes.Title>>(new None());
		}

		if (TraktClient.IsBlocked(response))
		{
			return Error.From(new RemoteUnavailableException(RemoteService.Trakt));
		}

		if (!response.IsSuccessStatusCode)
		{
			return response.StatusCode.Unanswered(RemoteService.Trakt, $"Trakt answered {(int)response.StatusCode} for {id}.");
		}

		return endpoint.StartsWith("search/", StringComparison.Ordinal)
			? Success.Of(await SearchedAsync(response, id.Type))
			: Success.Of(Optional.Of(await response.Content.ReadFromJsonAsync<TraktTypes.Title>()));
	}

	private static async Task<Optional<TraktTypes.Title>> SearchedAsync(HttpResponseMessage response, MediaType type)
	{
		var entries = await response.Content.ReadFromJsonAsync<List<TraktTypes.Item>>() ?? throw new InvalidDataException("Trakt answered an empty search.");
		return entries.Select(entry => Optional.Of(type is MediaType.Show ? entry.Show : entry.Movie)).WhereSome().FirstOrNone();
	}

	private readonly record struct Found(Task<Result<Optional<TraktTypes.Title>>> Answer, DateTimeOffset At);
}
