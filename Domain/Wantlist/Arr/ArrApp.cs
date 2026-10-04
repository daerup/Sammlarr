using Domain.Media;
using Domain.Providers;

namespace Domain.Wantlist.Arr;

public enum ArrApp
{
	Radarr,
	Sonarr,
}

public static class ArrApps
{
	extension(ArrApp app)
	{
		public string Name => app.Provider.ToString();

		public MediaProvider Provider
		{
			get
			{
				return app switch
				{
					ArrApp.Radarr => MediaProvider.Radarr,
					ArrApp.Sonarr => MediaProvider.Sonarr,
				};
			}
		}

		public MediaType Type
		{
			get
			{
				return app switch
				{
					ArrApp.Radarr => MediaType.Movie,
					ArrApp.Sonarr => MediaType.Show,
				};
			}
		}
	}
}
