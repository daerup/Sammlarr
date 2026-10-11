using System.Net;
using System.Text.Json.Nodes;
using API.Http;
using API.Http.Lanes;
using API.Mdblist.Types;
using Contracts.Auth;
using Domain.Auth;
using Domain.Media;
using Domain.Providers;
using Shared.Concurrency;

namespace API.Mdblist;

internal sealed class MdblistTitles(MdblistClient client, IApiKeyService apiKeys, Lanes lanes)
{
	private const string TitlesLane = "titles";

	private readonly Lock _gate = new();

	private readonly Dictionary<MdblistKey, Asked> _asked = [];

	public async Task<Answer<MdblistTypes.Titles.Title>> FindAsync(IReadOnlyCollection<IExternalMediaId> ids, CancellationToken cancellationToken = default)
	{
		var apiKey = await apiKeys.GetAsync(ApiKeyProvider.Mdblist, cancellationToken);
		return await apiKey.Match(
			usable => AskedAsync(usable, Keys(ids), cancellationToken),
			error => Task.FromResult(LaneAnswers.Failed<MdblistTypes.Titles.Title>(error, RemoteService.Mdblist))
		);
	}

	private async Task<Answer<MdblistTypes.Titles.Title>> AskedAsync(IApiKey apiKey, IReadOnlyList<MdblistKey> keys, CancellationToken cancellationToken)
	{
		if (keys.Count == 0)
		{
			return new NoMatch(RemoteService.Mdblist);
		}

		var answer = await Ask(apiKey, keys, cancellationToken);
		Alias(answer, keys[0].Type);
		return answer.ToAnswer(RemoteService.Mdblist);
	}

	private static List<MdblistKey> Keys(IReadOnlyCollection<IExternalMediaId> ids)
	{
		return [.. MdblistKey.Order.Select(source => ids.FirstOrNone(id => id.Source == source)).WhereSome().Select(MdblistKey.Of).WhereSome()];
	}

	private Task<LaneAnswer<MdblistTypes.Titles.Title>> Ask(IApiKey apiKey, IReadOnlyList<MdblistKey> keys, CancellationToken cancellationToken)
	{
		lock (_gate)
		{
			var held = keys.Select(_asked.GetValueOrNone).WhereSome().FirstOrNone(asked => asked.Usable);
			var asked = held.GetValueOr(() => new Asked(keys[0], Lane(apiKey).AskAsync(keys[0], Call(apiKey), cancellationToken)));
			Remember(keys, asked);
			return held.Match(found => Joined(apiKey, found, cancellationToken), () => asked.Answer);
		}
	}

	private Task<LaneAnswer<MdblistTypes.Titles.Title>> Joined(IApiKey apiKey, Asked found, CancellationToken cancellationToken)
	{
		return found.Answer.IsCompleted ? found.Answer : Lane(apiKey).AskAsync(found.Key, Call(apiKey), cancellationToken);
	}

	private void Remember(IEnumerable<MdblistKey> keys, Asked asked)
	{
		foreach (var key in keys.Where(key => _asked.GetValueOrNone(key).Match(held => !held.Usable, () => true)))
		{
			_asked[key] = asked;
		}
	}

	private void Alias(LaneAnswer<MdblistTypes.Titles.Title> answer, MediaType type)
	{
		if (answer is not LaneHit<MdblistTypes.Titles.Title> hit)
		{
			return;
		}

		lock (_gate)
		{
			var keys = MdblistKey.All(hit.Value, type).ToList();
			keys.FirstOrNone().Apply(first => Remember(keys, new Asked(first, Task.FromResult(answer))));
		}
	}

	private ILane<MdblistKey, MdblistTypes.Titles.Title> Lane(IApiKey apiKey)
	{
		return lanes.Of<MdblistKey, MdblistTypes.Titles.Title>(new LaneName(RemoteService.Mdblist, TitlesLane, apiKey.Value));
	}

	private LaneCall<MdblistKey, MdblistTypes.Titles.Title> Call(IApiKey apiKey)
	{
		return new LaneCall<MdblistKey, MdblistTypes.Titles.Title>(key => key.Batch, (batch, token) => PostAsync(apiKey, batch, token));
	}

	private async Task<Result<IReadOnlyDictionary<MdblistKey, MdblistTypes.Titles.Title>>> PostAsync(
		IApiKey apiKey,
		IReadOnlyList<MdblistKey> keys,
		CancellationToken cancellationToken
	)
	{
		var batch = keys[0];
		using var content = JsonContent.Create(new JsonObject { ["ids"] = new JsonArray([.. keys.Select(key => key.Encoded())]) });
		using var response = await client.SendAsync(HttpMethod.Post, batch.Endpoint, apiKey, cancellationToken, content);
		if (response.StatusCode is HttpStatusCode.NotFound)
		{
			return Success.Of<IReadOnlyDictionary<MdblistKey, MdblistTypes.Titles.Title>>(new Dictionary<MdblistKey, MdblistTypes.Titles.Title>());
		}

		if (!response.IsSuccessStatusCode)
		{
			return response.StatusCode.Unanswered(RemoteService.Mdblist, $"MDBList answered {(int)response.StatusCode} for {keys.Count} titles.");
		}

		var titles = await response.Content.ReadFromJsonAsync<List<MdblistTypes.Titles.Title>>(cancellationToken) ?? [];
		return Success.Of<IReadOnlyDictionary<MdblistKey, MdblistTypes.Titles.Title>>(Answered(titles, batch));
	}

	private static Dictionary<MdblistKey, MdblistTypes.Titles.Title> Answered(IEnumerable<MdblistTypes.Titles.Title> titles, MdblistKey batch)
	{
		return titles
			.SelectMany(title => MdblistKey.All(title, batch.Type).Where(key => key.Batch == batch.Batch).Select(key => (Key: key, Title: title)))
			.DistinctBy(pair => pair.Key)
			.ToDictionary(pair => pair.Key, pair => pair.Title);
	}

	private sealed record Asked(MdblistKey Key, Task<LaneAnswer<MdblistTypes.Titles.Title>> Answer)
	{
		public bool Usable => !Answer.IsCanceled && !Answer.IsFaulted && !(Answer.IsCompletedSuccessfully && Answer.Result is LaneFailed);
	}
}
