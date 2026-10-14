using System.Text.Json.Nodes;
using API.AniList.Types;
using API.Http;
using API.Http.Lanes;
using Contracts;
using Domain.Providers;

namespace API.AniList;

internal sealed class AniListTitles(AniListClient client, Lanes lanes, IDateTimeProvider clock)
{
	private const string Query = $"query ($ids: [Int]) {{ Page(perPage: 50) {{ media(id_in: $ids, type: ANIME) {{ {AniListMedia.Fields} }} }} }}";

	private static readonly LaneName Titles = new(RemoteService.AniList, "titles");

	private readonly RecentAnswers<int, Optional<AniListTypes.Media>> _read = new(clock);

	internal void Remember(AniListTypes.Media media) => _read.Remember(media.Id, Some.Of(media));

	internal Task<Result<Optional<AniListTypes.Media>>> FindAsync(int id, CancellationToken cancellationToken = default)
	{
		return _read.AskAsync(id, token => AskAsync(id, token), cancellationToken);
	}

	private async Task<Result<Optional<AniListTypes.Media>>> AskAsync(int id, CancellationToken cancellationToken)
	{
		var lane = lanes.Of<int, AniListTypes.Media>(Titles);
		var answer = await lane.AskAsync(id, new LaneCall<int, AniListTypes.Media>(_ => "anime", SendAsync), cancellationToken);
		return answer switch
		{
			LaneHit<AniListTypes.Media> hit => Success.Of<Optional<AniListTypes.Media>>(Some.Of(hit.Value)),
			LaneMiss => Success.Of<Optional<AniListTypes.Media>>(new None()),
			LaneFailed failed => failed.Cause,
		};
	}

	private async Task<Result<IReadOnlyDictionary<int, AniListTypes.Media>>> SendAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken)
	{
		var answer = await client.AskAsync<AniListTypes.PageData>(
			Query,
			new JsonObject { ["ids"] = new JsonArray([.. ids.Select(id => (JsonNode)id)]) },
			cancellationToken
		);
		return answer.Map(data =>
			(IReadOnlyDictionary<int, AniListTypes.Media>)(data.Page?.Items ?? []).DistinctBy(media => media.Id).ToDictionary(media => media.Id)
		);
	}
}
