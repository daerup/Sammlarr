using API.Arr.Radarr;
using API.Arr.Sonarr;
using Domain.Media;
using Domain.Wantlist.Arr;

namespace API.Arr;

internal sealed class ArrApis(RadarrApi radarr, SonarrApi sonarr)
{
	public IReadOnlyList<ArrApi> All => [radarr, sonarr];

	public ArrApi For(ArrApp app) => All.Single(api => api.App == app);

	public Optional<ArrApi> Taking(IExternalMediaId id) => All.FirstOrNone(api => api.Takes(id));
}
