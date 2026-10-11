using System.Text.Json.Nodes;
using API.AniList.Types;
using API.Anime;
using API.Http.Lanes;
using Domain.Providers;
using Shared.Concurrency;

namespace API.AniList;

internal sealed class AniListPrequels(AniListClient client, Lanes lanes) : IAnimePrequels
{
	private const string Query =
		"query ($ids: [Int]) { Page(perPage: 50) { media(idMal_in: $ids, type: ANIME) { idMal format relations { edges { relationType node { idMal type format } } } } } }";

	private static readonly LaneName Prequels = new(RemoteService.AniList, "prequels");

	private static readonly string[] SeasonFormats = ["TV", "TV_SHORT", "ONA"];

	public async Task<IReadOnlyDictionary<int, Result<Optional<int>>>> PrequelsOfAsync(IReadOnlyList<int> seasons, CancellationToken cancellationToken = default)
	{
		var lane = lanes.Of<int, int>(Prequels);
		var answers = await Task.WhenAll(lane.AskMany(seasons, new LaneCall<int, int>(_ => "anime", SendAsync), cancellationToken));
		return seasons.Zip(answers).ToDictionary(pair => pair.First, pair => Prequel(pair.Second));
	}

	private static Result<Optional<int>> Prequel(LaneAnswer<int> answer)
	{
		return answer switch
		{
			LaneHit<int> hit => Success.Of<Optional<int>>(Some.Of(hit.Value)),
			LaneMiss => Success.Of<Optional<int>>(new None()),
			LaneFailed failed => failed.Cause,
		};
	}

	private async Task<Result<IReadOnlyDictionary<int, int>>> SendAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken)
	{
		var answer = await client.AskAsync<AniListTypes.RelatedData>(
			Query,
			new JsonObject { ["ids"] = new JsonArray([.. ids.Select(id => (JsonNode)id)]) },
			cancellationToken
		);
		return answer.Map(data =>
			(IReadOnlyDictionary<int, int>)
				(data.Page?.Media ?? [])
					.Where(media => SeasonFormats.Contains(media.Format))
					.Select(media => Optional.Of(media.IdMal).Map(season => Prequel(media).Map(prequel => KeyValuePair.Create(season, prequel))))
					.WhereSome()
					.DistinctBy(pair => pair.Key)
					.ToDictionary()
		);
	}

	private static Optional<int> Prequel(AniListTypes.Related media)
	{
		return (media.Relations?.Edges ?? [])
			.FirstOrNone(edge => edge.RelationType == "PREQUEL" && edge.Node is { Type: "ANIME", IdMal: not null } && SeasonFormats.Contains(edge.Node.Format))
			.Map(edge => Optional.Of(edge.Node?.IdMal));
	}
}
