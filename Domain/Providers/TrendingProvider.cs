namespace Domain.Providers;

public enum TrendingProvider
{
	Tmdb,
}

public static class TrendingProviders
{
	extension(TrendingProvider trending)
	{
		public Service Service
		{
			get
			{
				return trending switch
				{
					TrendingProvider.Tmdb => RemoteService.Tmdb,
				};
			}
		}
	}
}
