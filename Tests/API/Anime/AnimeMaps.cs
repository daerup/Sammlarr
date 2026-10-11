using API.Anime;
using Contracts.Datasets;
using Domain.Media;

namespace API.Tests.Anime;

internal static class AnimeMaps
{
	internal static readonly AnimeIdMapping ApothecaryDiaries = new(
		MediaType.Show,
		Some.Of(161645),
		Some.Of(54492),
		Some.Of(431162),
		Some.Of(220542),
		Some.Of("tt28469390"),
		new None()
	);

	internal static readonly AnimeIdMapping ThirdSeason = new(
		MediaType.Show,
		Some.Of(185407),
		Some.Of(59130),
		new None(),
		new None(),
		new None(),
		Some.Of(431162)
	);

	internal static readonly string[] ApothecaryDiariesIds =
	[
		"AniList:Show:161645",
		"Mal:Show:54492",
		"Tvdb:Show:431162",
		"Tmdb:Show:220542",
		"Imdb:Show:tt28469390",
	];

	internal static Mock<IAnimeIds> Unknown()
	{
		var map = IAnimeIds.Mock();
		map.FindAsync(Any(), Any()).Returns(new None());
		map.FindManyAsync(Any(), Any()).Returns(new Dictionary<IExternalMediaId, AnimeIdMapping>());
		return map;
	}

	internal static Mock<IAnimeIds> KnowingTheApothecaryDiaries()
	{
		var map = IAnimeIds.Mock();
		map.FindAsync(Any(), Any()).Returns((id, _) => Find(id));
		map.FindManyAsync(Any(), Any()).Returns((ids, _) => FindMany(ids));
		return map;
	}

	internal static LaterSeasons Seasons(Mock<IAnimeIds> map, bool loaded = true)
	{
		var loads = Loads(DatasetNames.AnimeIds, loaded);
		return new LaterSeasons(new AnimeIdsInRun(map.Object, loads.Object), loads.Object);
	}

	internal static Mock<IDatasetLoads> Loads(string dataset, bool loaded = true)
	{
		var loads = IDatasetLoads.Mock();
		loads.FindAsync(dataset, Any()).Returns(Optional.When(loaded, () => new DatasetLoad(dataset, DateTimeOffset.UnixEpoch, new None(), 1)));
		return loads;
	}

	internal static IReadOnlyDictionary<IExternalMediaId, AnimeIdMapping> FindMany(IEnumerable<IExternalMediaId> ids)
	{
		return ids.Distinct().Select(id => Find(id).Map(entry => (Id: id, Entry: entry))).WhereSome().ToDictionary(found => found.Id, found => found.Entry);
	}

	private static Optional<AnimeIdMapping> Find(IExternalMediaId id)
	{
		return id switch
		{
			TvdbId { Value: "431162" } or AniListId { Value: "161645" } or MalId { Value: "54492" } => Some.Of(ApothecaryDiaries),
			AniListId { Value: "185407" } or MalId { Value: "59130" } => Some.Of(ThirdSeason),
			_ => new None(),
		};
	}
}
