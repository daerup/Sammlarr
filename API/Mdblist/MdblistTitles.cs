using System.Globalization;
using System.Text.Json.Nodes;
using API.Http;
using API.Mdblist.Types;
using Contracts.Auth;
using Domain.Auth;
using Domain.Media;
using Domain.Providers;

namespace API.Mdblist;

internal sealed class MdblistTitles(MdblistClient client, IApiKeyService apiKeys, MdblistBudget budget)
{
	private const int MostPerBatch = 100;

	private static readonly TimeSpan Gather = TimeSpan.FromMilliseconds(50);

	private readonly Lock _gate = new();

	private readonly Dictionary<BatchKey, Batch> _open = [];

	public Task<Result<Optional<MdblistTypes.Titles.Title>>> FindAsync(IExternalMediaId id, CancellationToken cancellationToken = default)
	{
		return BatchKey.Of(id).ToResult($"MDBList looks up no {id.Source} ids.").Map(key => AnswerAsync(key, id.Value, cancellationToken));
	}

	private async Task<Result<Optional<MdblistTypes.Titles.Title>>> AnswerAsync(BatchKey key, string value, CancellationToken cancellationToken)
	{
		var batch = Join(key, value);
		var titles = await batch.Answer.WaitAsync(cancellationToken);
		return titles.Map(found => found.FirstOrNone(title => MdblistIds.Of(title.Ids, key.Type).Any(id => id.Source == key.Source && id.Value == value)));
	}

	private Batch Join(BatchKey key, string value)
	{
		lock (_gate)
		{
			var batch = _open.GetValueOrNone(key).GetValueOr(() => Open(key));
			batch.Add(value);
			if (batch.Count >= MostPerBatch)
			{
				Close(key, batch);
			}

			return batch;
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
		budget.Record(apiKey.Value, response.Headers);
		if (!response.IsSuccessStatusCode)
		{
			return response.StatusCode.Unanswered(RemoteService.Mdblist, $"MDBList answered {(int)response.StatusCode} for {values.Count} titles.");
		}

		return Success.Of<IReadOnlyList<MdblistTypes.Titles.Title>>(await response.Content.ReadFromJsonAsync<List<MdblistTypes.Titles.Title>>() ?? []);
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
				MediaProvider.RottenTomatoes or MediaProvider.Plex or MediaProvider.Metacritic or MediaProvider.Letterboxd => false,
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
