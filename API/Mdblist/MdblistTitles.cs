using System.Globalization;
using System.Text.Json.Nodes;
using API.Http;
using API.Mdblist.Types;
using Contracts.Auth;
using Domain.Auth;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging;

namespace API.Mdblist;

internal sealed class MdblistTitles(MdblistClient client, IApiKeyService apiKeys, MdblistBudget budget, ILogger<MdblistTitles> logger)
{
	private const int MostPerBatch = 200;

	private static readonly TimeSpan Gather = TimeSpan.FromMilliseconds(50);

	private static readonly MediaProvider[] AskedFirst =
	[
		MediaProvider.Mdblist,
		MediaProvider.Imdb,
		MediaProvider.Tmdb,
		MediaProvider.Tvdb,
		MediaProvider.Trakt,
		MediaProvider.Mal,
	];

	private readonly Lock _gate = new();

	private readonly Dictionary<BatchKey, Batch> _open = [];

	private readonly Dictionary<TitleKey, Task<Result<Optional<MdblistTypes.Titles.Title>>>> _known = [];

	public Task<Result<Optional<MdblistTypes.Titles.Title>>> FindAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		return BatchKey.Of(id).ToResult($"MDBList looks up no {id.Source} ids.").Map(key => Ask(key, id, [id]).WaitAsync(cancellationToken));
	}

	public void Prefetch(IEnumerable<IReadOnlyCollection<IExternalMediaId>> titles)
	{
		foreach (var ids in titles)
		{
			AskedFirst.Select(ids.OfProvider).WhereSome().FirstOrNone().Apply(id => BatchKey.Of(id).Apply(key => Ask(key, id, ids)));
		}
	}

	private Task<Result<Optional<MdblistTypes.Titles.Title>>> Ask(BatchKey key, IExternalMediaId id, IReadOnlyCollection<IExternalMediaId> sameTitle)
	{
		lock (_gate)
		{
			return sameTitle.Select(each => _known.GetValueOrNone(TitleKey.Of(each))).WhereSome().FirstOrNone().GetValueOr(() => Join(key, id.Value, sameTitle));
		}
	}

	private Task<Result<Optional<MdblistTypes.Titles.Title>>> Join(BatchKey key, string value, IReadOnlyCollection<IExternalMediaId> sameTitle)
	{
		var batch = _open.GetValueOrNone(key).GetValueOr(() => Open(key));
		batch.Add(value);
		if (batch.Count >= MostPerBatch)
		{
			Close(key, batch);
		}

		var answer = MatchAsync(batch, key, value);
		foreach (var id in sameTitle)
		{
			_known[TitleKey.Of(id)] = answer;
		}

		_ = RememberAsync(answer, key.Type, sameTitle);
		return answer;
	}

	private static async Task<Result<Optional<MdblistTypes.Titles.Title>>> MatchAsync(Batch batch, BatchKey key, string value)
	{
		var titles = await batch.Answer;
		return titles.Map(found => found.FirstOrNone(title => MdblistIds.Of(title.Ids, key.Type).Any(id => id.Source == key.Source && id.Value == value)));
	}

	private async Task RememberAsync(Task<Result<Optional<MdblistTypes.Titles.Title>>> answer, MediaType type, IReadOnlyCollection<IExternalMediaId> asked)
	{
		await ((Task)answer).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
		Result<Optional<MdblistTypes.Titles.Title>> settled = answer.IsCompletedSuccessfully ? answer.Result : Error.From("MDBList's lookup threw.");
		lock (_gate)
		{
			settled.Apply(title => title.Apply(found => Remember(found, type, answer))).ApplyError(_ => Forget(asked, answer));
		}
	}

	private void Remember(MdblistTypes.Titles.Title title, MediaType type, Task<Result<Optional<MdblistTypes.Titles.Title>>> answer)
	{
		foreach (var id in MdblistIds.Of(title.Ids, type))
		{
			_known.TryAdd(TitleKey.Of(id), answer);
		}
	}

	private void Forget(IReadOnlyCollection<IExternalMediaId> asked, Task<Result<Optional<MdblistTypes.Titles.Title>>> answer)
	{
		foreach (var key in asked.Select(TitleKey.Of).Where(key => _known.GetValueOrNone(key).Match(held => held == answer, () => false)).ToList())
		{
			_known.Remove(key);
		}
	}

	private Batch Open(BatchKey key)
	{
		var batch = new Batch();
		_open[key] = batch;
		_ = CloseLaterAsync(key, batch);
		return batch;
	}

	private async Task CloseLaterAsync(BatchKey key, Batch batch)
	{
		await Task.Delay(Gather);
		lock (_gate)
		{
			Close(key, batch);
		}
	}

	private void Close(BatchKey key, Batch batch)
	{
		if (batch.Close())
		{
			_open.Remove(key);
			_ = SendAsync(key, batch);
		}
	}

	private async Task SendAsync(BatchKey key, Batch batch)
	{
		var asking = AskAsync(key, batch.Values);
		await ((Task)asking).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
		batch.Settle(asking);
	}

	private async Task<Result<IReadOnlyList<MdblistTypes.Titles.Title>>> AskAsync(BatchKey key, IReadOnlyList<string> values)
	{
		var apiKey = await apiKeys.GetAsync(ApiKeyProvider.Mdblist);
		return await apiKey
			.When(found => budget.LeavesReserve(found.Value), Error.From("MDBList's requests left today are kept for feeds."))
			.Map(found => PostAsync(found, key, values));
	}

	private async Task<Result<IReadOnlyList<MdblistTypes.Titles.Title>>> PostAsync(IApiKey apiKey, BatchKey key, IReadOnlyList<string> values)
	{
		using var content = JsonContent.Create(new JsonObject { ["ids"] = new JsonArray([.. values.Select(key.Encode)]) });
		using var response = await client.SendAsync(
			HttpMethod.Post,
			string.Format(CultureInfo.InvariantCulture, MdblistEndpoints.Titles, key.Segment, key.TypeSegment),
			apiKey,
			content: content
		);
		if (!response.IsSuccessStatusCode)
		{
			logger.LogWarning("MDBList answered {Status} to a lookup of {Count} titles", (int)response.StatusCode, values.Count);
			return response.StatusCode.Unanswered(RemoteService.Mdblist, $"MDBList answered {(int)response.StatusCode} for {values.Count} titles.");
		}

		return Success.Of<IReadOnlyList<MdblistTypes.Titles.Title>>(await response.Content.ReadFromJsonAsync<List<MdblistTypes.Titles.Title>>() ?? []);
	}

	private readonly record struct TitleKey(MediaProvider Source, MediaType Type, string Value)
	{
		public static TitleKey Of(IExternalMediaId id) => new(id.Source, id.Type, id.Value);
	}

	private readonly record struct BatchKey(MediaProvider Source, MediaType Type)
	{
		public static Optional<BatchKey> Of(IExternalMediaId id) => Optional.When(IsLookedUp(id.Source), () => new BatchKey(id.Source, id.Type));

		public string Segment => Source.ToString().ToLowerInvariant();

		public string TypeSegment
		{
			get
			{
				return Type switch
				{
					MediaType.Movie => "movie",
					MediaType.Show => "show",
					MediaType.Unknown => "any",
				};
			}
		}

		public JsonNode Encode(string value)
		{
			return Source is MediaProvider.Imdb or MediaProvider.Mdblist || !long.TryParse(value, out var number)
				? JsonValue.Create(value)
				: JsonValue.Create(number);
		}

		private static bool IsLookedUp(MediaProvider source)
		{
			return source switch
			{
				MediaProvider.Imdb or MediaProvider.Tmdb or MediaProvider.Tvdb or MediaProvider.Trakt or MediaProvider.Mal or MediaProvider.Mdblist => true,
				MediaProvider.RottenTomatoes
				or MediaProvider.Plex
				or MediaProvider.Metacritic
				or MediaProvider.Letterboxd
				or MediaProvider.Tvmaze
				or MediaProvider.AniList => false,
			};
		}
	}

	private sealed class Batch
	{
		private readonly List<string> _values = [];

		private readonly TaskCompletionSource<Result<IReadOnlyList<MdblistTypes.Titles.Title>>> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

		private bool _closed;

		public int Count => _values.Count;

		public IReadOnlyList<string> Values => _values;

		public Task<Result<IReadOnlyList<MdblistTypes.Titles.Title>>> Answer => _answer.Task;

		public void Add(string value)
		{
			if (!_values.Contains(value))
			{
				_values.Add(value);
			}
		}

		public bool Close()
		{
			var closing = !_closed;
			_closed = true;
			return closing;
		}

		public void Settle(Task<Result<IReadOnlyList<MdblistTypes.Titles.Title>>> asked) => _answer.SetFromTask(asked);
	}
}
