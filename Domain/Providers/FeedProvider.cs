namespace Domain.Providers;

public enum FeedProvider
{
	Mdblist,
	Tmdb,
	AniList,
	Mal,
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
					FeedProvider.AniList => RemoteService.AniList,
					FeedProvider.Mal => RemoteService.Mal,
				};
			}
		}
	}
}
