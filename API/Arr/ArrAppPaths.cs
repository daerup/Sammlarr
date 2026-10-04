using Domain.Wantlist.Arr;

namespace API.Arr;

internal static class ArrAppPaths
{
	extension(ArrApp app)
	{
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
