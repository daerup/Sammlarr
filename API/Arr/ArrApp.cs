using Domain.Media;
using Domain.Providers;

namespace API.Arr;

internal enum ArrApp
{
	Radarr,
	Sonarr,
}

internal static class ArrApps
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

		public string Titles
		{
			get
			{
				return app switch
				{
					ArrApp.Radarr => "movie",
					ArrApp.Sonarr => "series",
				};
			}
		}

		public string TitleIds
		{
			get
			{
				return app switch
				{
					ArrApp.Radarr => "movieIds",
					ArrApp.Sonarr => "seriesIds",
				};
			}
		}

		public string ExclusionFlag
		{
			get
			{
				return app switch
				{
					ArrApp.Radarr => "addImportExclusion",
					ArrApp.Sonarr => "addImportListExclusion",
				};
			}
		}

		public string LookupTerm
		{
			get
			{
				return app switch
				{
					ArrApp.Radarr => "tmdb",
					ArrApp.Sonarr => "tvdb",
				};
			}
		}
	}
}
