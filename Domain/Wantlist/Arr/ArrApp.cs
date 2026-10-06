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
	public static Optional<ArrApp> For(MediaType type) => Enum.GetValues<ArrApp>().FirstOrNone(app => app.Type == type);

	extension(ArrApp app)
	{
		public string Name => app.ToString();

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

		public IReadOnlyList<MediaProvider> SearchableBy
		{
			get
			{
				return app switch
				{
					ArrApp.Radarr => [MediaProvider.Tmdb, MediaProvider.Imdb],
					ArrApp.Sonarr => [MediaProvider.Tvdb, MediaProvider.Tmdb, MediaProvider.Imdb],
				};
			}
		}

		public int DefaultPort
		{
			get
			{
				return app switch
				{
					ArrApp.Radarr => 7878,
					ArrApp.Sonarr => 8989,
				};
			}
		}
	}
}
