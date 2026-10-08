using Domain.Providers;

namespace Contracts.Datasets;

public static class DatasetNames
{
	public const string ImdbRatings = "imdb-ratings";

	public const string AnimeIds = "anime-ids";

	public static readonly IReadOnlyDictionary<RemoteService, string> ByService = new Dictionary<RemoteService, string>
	{
		[RemoteService.ImdbDatasets] = ImdbRatings,
		[RemoteService.AnimeLists] = AnimeIds,
	};
}
