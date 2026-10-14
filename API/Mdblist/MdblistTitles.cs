using System.Net;
using System.Text.Json.Nodes;
using API.Http;
using API.Http.Lanes;
using API.Mdblist.Types;
using Contracts;
using Contracts.Auth;
using Contracts.Media;
using Domain.Auth;
using Domain.Media;
using Domain.Providers;

namespace API.Mdblist;

internal sealed class MdblistTitles(MdblistClient client, IApiKeyService apiKeys, Lanes lanes, IDateTimeProvider clock) : IBurstPrimer
{
	private const string TitlesLane = "titles";

	private static readonly TimeSpan FailureKept = TimeSpan.FromMinutes(1);

	private readonly Lock _gate = new();

	private readonly Dictionary<MdblistKey, Asked> _asked = [];

	public RemoteService Remote => RemoteService.Mdblist;

	public async Task PrimeAsync(IReadOnlyCollection<IReadOnlyList<IExternalMediaId>> titles, CancellationToken cancellationToken)
	{
		var apiKey = await apiKeys.GetAsync(ApiKeyProvider.Mdblist, cancellationToken);
		apiKey.Apply(usable => Prime(usable, [.. titles.Select(Keys).Where(keys => keys.Count > 0)], cancellationToken));
	}

	public async Task<Answer<MdblistTypes.Titles.Title>> FindAsync(IReadOnlyCollection<IExternalMediaId> ids, CancellationToken cancellationToken = default)
	{
		var apiKey = await apiKeys.GetAsync(ApiKeyProvider.Mdblist, cancellationToken);
		return await apiKey.Match(
			usable => AskedAsync(usable, Keys(ids), cancellationToken),
			error => LaneAnswers.Failed<MdblistTypes.Titles.Title>(error, RemoteService.Mdblist)
		);
	}

	private void Prime(IApiKey apiKey, IReadOnlyList<List<MdblistKey>> titles, CancellationToken cancellationToken)
	{
		lock (_gate)
		{
			List<List<MdblistKey>> fresh = [.. titles.Where(keys => keys.All(Open))];
			var answers = Lane(apiKey).AskMany([.. fresh.Select(keys => keys[0])], Call(apiKey), cancellationToken);
			foreach (var (keys, answer) in fresh.Zip(answers))
			{
				Remember(keys, Asking(keys[0], answer));
			}
		}
	}

	private async Task<Answer<MdblistTypes.Titles.Title>> AskedAsync(IApiKey apiKey, IReadOnlyList<MdblistKey> keys, CancellationToken cancellationToken)
	{
		if (keys.Count == 0)
		{
			return new NoMatch(RemoteService.Mdblist);
		}

		var (asked, asking) = Ask(apiKey, keys, cancellationToken);
		var answer = await asking;
		if (answer is LaneMiss && keys[0].Outranks(asked.Key))
		{
			answer = await AskByBest(apiKey, keys[0], asked, cancellationToken);
		}

		Alias(answer, keys);
		return answer.ToAnswer(RemoteService.Mdblist);
	}

	private static List<MdblistKey> Keys(IReadOnlyCollection<IExternalMediaId> ids)
	{
		return [.. MdblistKey.Order.Select(source => ids.FirstOrNone(id => id.Source == source)).WhereSome().Select(MdblistKey.Of).WhereSome()];
	}

	private (Asked Asked, Task<LaneAnswer<MdblistTypes.Titles.Title>> Answer) Ask(
		IApiKey apiKey,
		IReadOnlyList<MdblistKey> keys,
		CancellationToken cancellationToken
	)
	{
		lock (_gate)
		{
			var (asked, answer) = Share(apiKey, keys.Select(_asked.GetValueOrNone).WhereSome().FirstOrNone(Usable), keys[0], cancellationToken);
			Remember(keys.Where(Open), asked);
			return (asked, answer);
		}
	}

	private Task<LaneAnswer<MdblistTypes.Titles.Title>> AskByBest(IApiKey apiKey, MdblistKey best, Asked missed, CancellationToken cancellationToken)
	{
		lock (_gate)
		{
			var (asked, answer) = Share(apiKey, _asked.GetValueOrNone(best).When(held => held != missed && Usable(held)), best, cancellationToken);
			Remember([best], asked);
			return answer;
		}
	}

	private (Asked Asked, Task<LaneAnswer<MdblistTypes.Titles.Title>> Answer) Share(
		IApiKey apiKey,
		Optional<Asked> held,
		MdblistKey key,
		CancellationToken cancellationToken
	)
	{
		var asked = held.GetValueOr(() => Asking(key, Lane(apiKey).AskAsync(key, Call(apiKey), cancellationToken)));
		return (asked, held.Match(found => Joined(apiKey, found, cancellationToken), () => asked.Answer));
	}

	private Task<LaneAnswer<MdblistTypes.Titles.Title>> Joined(IApiKey apiKey, Asked found, CancellationToken cancellationToken)
	{
		return found.Answer.IsCompletedSuccessfully
			? found.Answer
			: Lane(apiKey).Join(found.Key, cancellationToken).GetValueOr(() => SettledOrAskedAsync(apiKey, found, cancellationToken));
	}

	private async Task<LaneAnswer<MdblistTypes.Titles.Title>> SettledOrAskedAsync(IApiKey apiKey, Asked found, CancellationToken cancellationToken)
	{
		await ((Task)found.Answer).WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
		return found.Answer.IsCompletedSuccessfully ? await found.Answer : await Lane(apiKey).AskAsync(found.Key, Call(apiKey), cancellationToken);
	}

	private void Remember(IEnumerable<MdblistKey> keys, Asked asked)
	{
		foreach (var key in keys)
		{
			_asked[key] = asked;
		}
	}

	private bool Open(MdblistKey key)
	{
		return _asked.GetValueOrNone(key).Match(held => !Usable(held), () => true);
	}

	private bool Missed(MdblistKey key)
	{
		return _asked.GetValueOrNone(key).Match(held => held.Answer.IsCompletedSuccessfully && held.Answer.Result is LaneMiss, () => false);
	}

	private void Alias(LaneAnswer<MdblistTypes.Titles.Title> answer, IReadOnlyList<MdblistKey> keys)
	{
		if (answer is not LaneHit<MdblistTypes.Titles.Title> hit)
		{
			return;
		}

		lock (_gate)
		{
			List<MdblistKey> known = [.. keys.Concat(MdblistKey.All(hit.Value, keys[0].Type)).Distinct().Where(key => Open(key) || Missed(key))];
			Remember(known, Asking(keys[0], Task.FromResult(answer)));
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
		using var response = await client.SendAsync(HttpMethod.Post, batch.Endpoint, apiKey, content, cancellationToken);
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

	private bool Usable(Asked asked)
	{
		if (asked.Answer.IsCanceled || asked.Answer.IsFaulted)
		{
			return false;
		}

		if (!asked.Answer.IsCompletedSuccessfully || asked.Answer.Result is not LaneFailed failed)
		{
			return true;
		}

		var sent = failed.Cause.CausedBy<RemoteUnavailableException>().Map(unavailable => unavailable.Sent).GetValueOr(true);
		return sent && clock.Now - asked.SettledAt < FailureKept;
	}

	private Asked Asking(MdblistKey key, Task<LaneAnswer<MdblistTypes.Titles.Title>> answer)
	{
		return new Asked(key, answer, clock);
	}

	private sealed class Asked
	{
		public Asked(MdblistKey key, Task<LaneAnswer<MdblistTypes.Titles.Title>> answer, IDateTimeProvider clock)
		{
			Key = key;
			Answer = SettledAsync(answer, clock);
		}

		public MdblistKey Key { get; }

		public Task<LaneAnswer<MdblistTypes.Titles.Title>> Answer { get; }

		public DateTimeOffset SettledAt { get; private set; }

		private async Task<LaneAnswer<MdblistTypes.Titles.Title>> SettledAsync(Task<LaneAnswer<MdblistTypes.Titles.Title>> answer, IDateTimeProvider clock)
		{
			await ((Task)answer).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
			SettledAt = clock.Now;
			return await answer;
		}
	}
}
