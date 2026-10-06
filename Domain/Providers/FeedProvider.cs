namespace Domain.Providers;

public enum FeedProvider
{
	Tmdb,
	Mdblist,
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
					FeedProvider.Tmdb => RemoteService.Tmdb,
					FeedProvider.Mdblist => RemoteService.Mdblist,
				};
			}
		}
	}
}
