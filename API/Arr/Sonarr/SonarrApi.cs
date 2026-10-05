using System.Text.Json.Nodes;
using API.Arr.Types;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Arr;
using Shared.Options;

namespace API.Arr.Sonarr;

internal sealed class SonarrApi(HttpClient http, SonarrOptions options) : ArrApi(http)
{
	public override ArrApp App => ArrApp.Sonarr;

	public override MediaProvider AddressedBy => MediaProvider.Tvdb;

	protected override string Titles => "series";

	protected override string TitleIds => "seriesIds";

	protected override string ExclusionFlag => "addImportListExclusion";

	protected override string LookupTerm => "tvdb";

	public override int AddressOf(ArrTitle title) => title.TvdbId;

	public override IReadOnlyList<IExternalMediaId> IdsOf(ArrTitle title)
	{
		var tvdb = TvdbId.From(title.TvdbId.ToString(), MediaType.Show);
		return title.TmdbId > 0 ? [tvdb, TmdbId.From(title.TmdbId.ToString(), MediaType.Show)] : [tvdb];
	}

	protected override void AddOptions(JsonObject title, ArrInstanceConfig settings)
	{
		title["seasonFolder"] = options.SeasonFolder;
		title["seriesType"] = Wire(options.SeriesType);
		title["addOptions"] = new JsonObject
		{
			["monitor"] = settings.Monitored ? "all" : "none",
			["searchForMissingEpisodes"] = settings.SearchOnAdd,
			["searchForCutoffUnmetEpisodes"] = false,
		};
	}
}
