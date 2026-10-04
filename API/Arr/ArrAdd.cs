using System.Text.Json.Nodes;
using Contracts.Settings;

namespace API.Arr;

internal static class ArrAdd
{
	public static JsonObject Prepare(ArrApp app, JsonObject lookup, ArrInstanceConfig settings, ArrAddition addition)
	{
		lookup["qualityProfileId"] = addition.QualityProfileId;
		lookup["rootFolderPath"] = addition.RootFolderPath;
		lookup["monitored"] = settings.Monitored;
		lookup["tags"] = new JsonArray([.. addition.Tags.Select(tag => JsonValue.Create(tag))]);
		return app switch
		{
			ArrApp.Radarr => Movie(lookup, settings),
			ArrApp.Sonarr => Series(lookup, settings),
		};
	}

	private static JsonObject Movie(JsonObject lookup, ArrInstanceConfig settings)
	{
		lookup["minimumAvailability"] = "released";
		lookup["addOptions"] = new JsonObject { ["searchForMovie"] = settings.SearchOnAdd, ["monitor"] = settings.Monitored ? "movieOnly" : "none" };
		return lookup;
	}

	private static JsonObject Series(JsonObject lookup, ArrInstanceConfig settings)
	{
		lookup["seasonFolder"] = true;
		lookup["seriesType"] = "standard";
		lookup["addOptions"] = new JsonObject
		{
			["monitor"] = settings.Monitored ? "all" : "none",
			["searchForMissingEpisodes"] = settings.SearchOnAdd,
			["searchForCutoffUnmetEpisodes"] = false,
		};
		return lookup;
	}
}

internal sealed record ArrAddition(int QualityProfileId, string RootFolderPath, IReadOnlyList<int> Tags);
