using System.Text.Json.Nodes;
using API.Arr.Types;
using Contracts.Settings;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Arr;
using Shared.Options;

namespace API.Arr.Radarr;

internal sealed class RadarrApi(HttpClient http, RadarrOptions options) : ArrApi(http)
{
	public override ArrApp App => ArrApp.Radarr;

	public override MediaProvider AddressedBy => MediaProvider.Tmdb;

	protected override string Titles => "movie";

	protected override string TitleIds => "movieIds";

	protected override string ExclusionFlag => "addImportExclusion";

	protected override string LookupTerm => "tmdb";

	public override int AddressOf(ArrTitle title) => title.TmdbId;

	public override IReadOnlyList<IExternalMediaId> IdsOf(ArrTitle title) => [TmdbId.From(title.TmdbId.ToString(), MediaType.Movie)];

	protected override void AddOptions(JsonObject title, ArrInstanceConfig settings)
	{
		title["minimumAvailability"] = Wire(options.MinimumAvailability);
		title["addOptions"] = new JsonObject { ["searchForMovie"] = settings.SearchOnAdd, ["monitor"] = settings.Monitored ? "movieOnly" : "none" };
	}
}
