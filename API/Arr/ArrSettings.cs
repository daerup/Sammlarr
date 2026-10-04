using Contracts.Settings;
using Domain.Wantlist.Arr;

namespace API.Arr;

internal static class ArrSettings
{
	internal static ArrInstanceConfig Of(this ArrSinkConfig config, ArrApp app)
	{
		return app switch
		{
			ArrApp.Radarr => config.Radarr,
			ArrApp.Sonarr => config.Sonarr,
		};
	}
}
