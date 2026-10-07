using System.Text.Json.Nodes;
using API.AniList.Types;
using Contracts;

namespace API.AniList;

internal sealed class AniListTitles(AniListClient client, IDateTimeProvider clock)
{
	private const int MostPerBatch = 50;

	private const string Query = $"query ($ids: [Int]) {{ Page(perPage: 50) {{ media(id_in: $ids, type: ANIME) {{ {AniListMedia.Fields} }} }} }}";

	private static readonly TimeSpan Kept = TimeSpan.FromMinutes(10);

	private static readonly TimeSpan Gather = TimeSpan.FromMilliseconds(50);

	private readonly Lock _gate = new();

	private readonly Dictionary<int, Read> _read = [];

	private readonly Dictionary<int, Task<Result<Optional<AniListTypes.Media>>>> _pending = [];

	private Optional<Batch> _open = new None();

	internal void Remember(AniListTypes.Media media)
	{
		lock (_gate)
		{
			foreach (var stale in _read.Where(read => clock.Now - read.Value.At >= Kept).Select(read => read.Key).ToList())
			{
				_read.Remove(stale);
			}

			_read[media.Id] = new Read(media, clock.Now);
		}
	}

	internal Task<Result<Optional<AniListTypes.Media>>> FindAsync(int id, CancellationToken cancellationToken = default)
	{
		lock (_gate)
		{
			return _read.GetValueOrNone(id).When(read => clock.Now - read.At < Kept).Match(read => Known(read.Media), () => Join(id)).WaitAsync(cancellationToken);
		}
	}

	private static Task<Result<Optional<AniListTypes.Media>>> Known(AniListTypes.Media media)
	{
		return Task.FromResult<Result<Optional<AniListTypes.Media>>>(Success.Of<Optional<AniListTypes.Media>>(Some.Of(media)));
	}

	private Task<Result<Optional<AniListTypes.Media>>> Join(int id) => _pending.GetValueOrNone(id).GetValueOr(() => Enter(id));

	private Task<Result<Optional<AniListTypes.Media>>> Enter(int id)
	{
		var batch = _open.GetValueOr(Open);
		batch.Add(id);
		if (batch.Count >= MostPerBatch)
		{
			Close(batch);
		}

		var answer = MatchAsync(batch, id);
		_pending[id] = answer;
		return answer;
	}

	private static async Task<Result<Optional<AniListTypes.Media>>> MatchAsync(Batch batch, int id)
	{
		var answer = await batch.Answer;
		return answer.Map(found => found.FirstOrNone(media => media.Id == id));
	}

	private Batch Open()
	{
		var batch = new Batch();
		_open = Some.Of(batch);
		_ = CloseLaterAsync(batch);
		return batch;
	}

	private async Task CloseLaterAsync(Batch batch)
	{
		await Task.Delay(Gather);
		lock (_gate)
		{
			Close(batch);
		}
	}

	private void Close(Batch batch)
	{
		if (batch.Close())
		{
			_open = new None();
			_ = SendAsync(batch);
		}
	}

	private async Task SendAsync(Batch batch)
	{
		var asking = AskAsync(batch.Ids);
		await ((Task)asking).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
		batch.Settle(asking);
		lock (_gate)
		{
			foreach (var id in batch.Ids)
			{
				_pending.Remove(id);
			}
		}
	}

	private async Task<Result<IReadOnlyList<AniListTypes.Media>>> AskAsync(IReadOnlyList<int> ids)
	{
		var answer = await client.AskAsync<AniListTypes.PageData>(Query, new JsonObject { ["ids"] = new JsonArray([.. ids.Select(id => (JsonNode)id)]) });
		return answer.Map(data => (IReadOnlyList<AniListTypes.Media>)[.. data.Page?.Media ?? []]).Apply(found => found.ToList().ForEach(Remember));
	}

	private readonly record struct Read(AniListTypes.Media Media, DateTimeOffset At);

	private sealed class Batch
	{
		private readonly List<int> _ids = [];

		private readonly TaskCompletionSource<Result<IReadOnlyList<AniListTypes.Media>>> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

		private bool _closed;

		public int Count => _ids.Count;

		public IReadOnlyList<int> Ids => _ids;

		public Task<Result<IReadOnlyList<AniListTypes.Media>>> Answer => _answer.Task;

		public void Add(int id)
		{
			if (!_ids.Contains(id))
			{
				_ids.Add(id);
			}
		}

		public bool Close()
		{
			var closing = !_closed;
			_closed = true;
			return closing;
		}

		public void Settle(Task<Result<IReadOnlyList<AniListTypes.Media>>> asked) => _answer.SetFromTask(asked);
	}
}
