using Domain.Wantlist.Arr;

namespace API.Arr;

internal sealed class ArrClients(RadarrClient radarr, SonarrClient sonarr)
{
	public ArrClient For(ArrApp app)
	{
		return app switch
		{
			ArrApp.Radarr => radarr,
			ArrApp.Sonarr => sonarr,
		};
	}
}
