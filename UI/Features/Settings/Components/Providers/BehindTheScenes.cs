using Contracts.Datasets;
using Domain.Providers;
using UI.Features.Settings.Components.Usage;

namespace UI.Features.Settings.Components.Providers;

internal sealed record BehindTheScenesStatus(string Text, bool Ready);

internal static class BehindTheScenes
{
	internal static readonly IReadOnlyList<(RemoteService Service, string Does)> Services =
	[
		(RemoteService.Wikidata, "Translates ids between services"),
		(RemoteService.Tvmaze, "Show ids and details"),
		(RemoteService.AnimeLists, "Anime ids for TMDB and TheTVDB, a daily file"),
		(RemoteService.ImdbDatasets, "IMDb ratings, a daily file"),
	];

	internal static BehindTheScenesStatus Status(RemoteService service, IReadOnlyDictionary<RemoteService, DateTimeOffset> downloads, DateTimeOffset now)
	{
		if (!DatasetNames.ByService.ContainsKey(service))
		{
			return new BehindTheScenesStatus("Ready", true);
		}

		return downloads
			.GetValueOrNone(service)
			.Map(at => new BehindTheScenesStatus(Downloaded(at, now), true))
			.GetValueOr(new BehindTheScenesStatus("Not downloaded yet", false));
	}

	private static string Downloaded(DateTimeOffset at, DateTimeOffset now)
	{
		var sameDay = at.ToLocalTime().Date == now.ToLocalTime().Date;
		return sameDay ? $"Downloaded at {UsageView.Clock(at, now)}" : $"Downloaded {UsageView.Clock(at, now)}";
	}
}
