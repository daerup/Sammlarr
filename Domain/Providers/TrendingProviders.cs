namespace Domain.Providers;

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
