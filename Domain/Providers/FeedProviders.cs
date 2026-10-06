namespace Domain.Providers;

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
