namespace Domain.Providers;

public enum FeedProvider
{
	Mdblist,
	Tmdb,
}

public static class FeedProviders
{
	extension(FeedProvider feed)
	{
		public Service Service
		{
			get
			{
				return feed switch
				{
					FeedProvider.Mdblist => RemoteService.Mdblist,
					FeedProvider.Tmdb => RemoteService.Tmdb,
				};
			}
		}
	}
}
